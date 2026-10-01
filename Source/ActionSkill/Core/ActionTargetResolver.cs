using System.Collections.Generic;
using UnityEngine;

public static class ActionTargetResolver
{
    /*
    Action의 TargetMode, Filter, AreaShape와 Layer 설정을 기준으로
    CombatTarget을 탐색하고 ActionEffectTarget 목록으로 변환하는 Resolver 클래스
    */

    private const float MinimumRadius = 0.1f;
    private const float MinimumSectorAngle = 1f;
    private const float MaximumSectorAngle = 360f;

    //원형 범위를 기본값으로 사용하여 대상 탐색
    public static void ResolveTargets(
        ActionUserContext a_Context,
        ActionTargetMode a_TargetMode,
        ActionTargetFilter a_TargetFilter,
        Vector2 a_ImpactPosition,
        GameObject a_HitObject,
        float a_Radius,
        LayerMask a_TargetLayerMask,
        List<ActionEffectTarget> a_ResultTargets,
        Collider2D[] a_OverlapBuffer,
        HashSet<GameObject> a_AddedObjects,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        ResolveTargets(
            a_Context,
            a_TargetMode,
            a_TargetFilter,
            ActionAreaShape.Circle,
            a_ImpactPosition,
            a_HitObject,
            a_Radius,
            MaximumSectorAngle,
            Vector2.right,
            a_TargetLayerMask,
            a_ResultTargets,
            a_OverlapBuffer,
            a_AddedObjects,
            a_TargetCache
        );
    } //public static void ResolveTargets()

    //지정된 Target 및 Area 설정을 기준으로 Action 대상 탐색
    public static void ResolveTargets(
        ActionUserContext a_Context,
        ActionTargetMode a_TargetMode,
        ActionTargetFilter a_TargetFilter,
        ActionAreaShape a_AreaShape,
        Vector2 a_ImpactPosition,
        GameObject a_HitObject,
        float a_Radius,
        float a_SectorAngle,
        Vector2 a_UseDirection,
        LayerMask a_TargetLayerMask,
        List<ActionEffectTarget> a_ResultTargets,
        Collider2D[] a_OverlapBuffer,
        HashSet<GameObject> a_AddedObjects,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_ResultTargets == null)
            return;

        a_ResultTargets.Clear();
        a_AddedObjects?.Clear();

        if (a_Context == null)
            return;

        Vector2 useDirection = GetValidDirection(a_UseDirection);

        switch (a_TargetMode)
        {
            case ActionTargetMode.Caster:
                AddCasterTarget(
                    a_Context,
                    a_TargetFilter,
                    a_ResultTargets,
                    a_TargetCache
                );
                break;

            case ActionTargetMode.CasterRadius:
                ResolveAreaTargets(
                    a_Context,
                    a_Context.GetCasterPosition(),
                    a_Radius,
                    a_AreaShape,
                    a_SectorAngle,
                    useDirection,
                    a_TargetLayerMask,
                    a_TargetFilter,
                    a_ResultTargets,
                    a_OverlapBuffer,
                    a_AddedObjects,
                    a_TargetCache
                );
                break;

            case ActionTargetMode.HitObject:
                AddObjectTarget(
                    a_Context,
                    a_HitObject,
                    a_TargetFilter,
                    a_ResultTargets,
                    a_TargetCache
                );
                break;

            case ActionTargetMode.ImpactRadius:
                ResolveAreaTargets(
                    a_Context,
                    a_ImpactPosition,
                    a_Radius,
                    a_AreaShape,
                    a_SectorAngle,
                    useDirection,
                    a_TargetLayerMask,
                    a_TargetFilter,
                    a_ResultTargets,
                    a_OverlapBuffer,
                    a_AddedObjects,
                    a_TargetCache
                );
                break;
        }
    } //public static void ResolveTargets()

    //Resolve Request와 재사용 Buffer를 이용하여 Action 대상 탐색
    public static void ResolveTargets(
        ActionUserContext a_Context,
        ActionTargetResolveRequest a_Request,
        ActionTargetResolveBuffer a_Buffer)
    {
        if (a_Buffer == null)
            return;

        a_Buffer.ClearTargets();

        if (a_Context == null)
            return;

        ResolveTargets(
            a_Context,
            a_Request.m_TargetMode,
            a_Request.m_TargetFilter,
            a_Request.m_AreaShape,
            a_Request.m_ImpactPosition,
            a_Request.m_HitObject,
            a_Request.m_Radius,
            a_Request.m_SectorAngle,
            a_Request.m_UseDirection,
            a_Request.m_TargetLayerMask,
            a_Buffer.Targets,
            a_Buffer.OverlapBuffer,
            a_Buffer.AddedObjects,
            a_Buffer.TargetCache
        );
    } //public static void ResolveTargets()

    //Caster를 Action 대상 후보로 변환하고 Filter 검증 후 추가
    private static void AddCasterTarget(
        ActionUserContext a_Context,
        ActionTargetFilter a_TargetFilter,
        List<ActionEffectTarget> a_Targets,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_Context.m_CasterObject == null)
            return;

        ActionEffectTarget target = CreateTargetFromObject(
            a_Context.m_CasterObject,
            a_TargetCache
        );

        if (target == null || IsValidTarget(a_Context, target, a_TargetFilter) == false)
            return;

        a_Targets.Add(target);
    } //private static void AddCasterTarget()

    //지정된 GameObject를 Action 대상으로 변환하고 Filter 검증 후 추가
    private static void AddObjectTarget(
        ActionUserContext a_Context,
        GameObject a_TargetObject,
        ActionTargetFilter a_TargetFilter,
        List<ActionEffectTarget> a_Targets,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_TargetObject == null)
            return;

        ActionEffectTarget target = CreateTargetFromObject(
            a_TargetObject,
            a_TargetCache
        );

        if (target == null || IsValidTarget(a_Context, target, a_TargetFilter) == false)
            return;

        a_Targets.Add(target);
    } //private static void AddObjectTarget()

    //지정된 중심과 범위 안의 Collider를 탐색하여 유효한 Action 대상 추가
    private static void ResolveAreaTargets(
        ActionUserContext a_Context,
        Vector2 a_Center,
        float a_Radius,
        ActionAreaShape a_AreaShape,
        float a_SectorAngle,
        Vector2 a_UseDirection,
        LayerMask a_TargetLayerMask,
        ActionTargetFilter a_TargetFilter,
        List<ActionEffectTarget> a_Targets,
        Collider2D[] a_OverlapBuffer,
        HashSet<GameObject> a_AddedObjects,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_OverlapBuffer == null || a_OverlapBuffer.Length == 0)
            return;

        int layerMask = a_TargetLayerMask.value;

        if (layerMask == 0)
            layerMask = Physics2D.AllLayers;

        ContactFilter2D contactFilter = new ContactFilter2D();

        contactFilter.SetLayerMask(layerMask);
        contactFilter.useTriggers = Physics2D.queriesHitTriggers;

        int hitCount = Physics2D.OverlapCircle(
            a_Center,
            Mathf.Max(MinimumRadius, a_Radius),
            contactFilter,
            a_OverlapBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = a_OverlapBuffer[i];
            a_OverlapBuffer[i] = null;

            if (hitCollider == null)
                continue;

            ActionEffectTarget target = CreateTargetFromCollider(
                hitCollider,
                a_TargetCache
            );

            if (target == null || target.HasTarget() == false)
                continue;

            GameObject targetObject = target.GetGameObject();

            if (IsTargetAlreadyAdded(
                    targetObject,
                    a_Targets,
                    a_AddedObjects))
            {
                continue;
            }

            if (IsInsideAreaShape(
                    target,
                    a_Center,
                    a_AreaShape,
                    a_SectorAngle,
                    a_UseDirection) == false)
            {
                continue;
            }

            if (IsValidTarget(
                    a_Context,
                    target,
                    a_TargetFilter) == false)
            {
                continue;
            }

            a_AddedObjects?.Add(targetObject);
            a_Targets.Add(target);
        }
    } //private static void ResolveAreaTargets()

    //대상이 현재 Resolve 결과에 이미 추가되어 있는지 확인
    private static bool IsTargetAlreadyAdded(
        GameObject a_TargetObject,
        List<ActionEffectTarget> a_Targets,
        HashSet<GameObject> a_AddedObjects)
    {
        if (a_TargetObject == null)
            return true;

        if (a_AddedObjects != null)
            return a_AddedObjects.Contains(a_TargetObject);

        for (int i = 0; i < a_Targets.Count; i++)
        {
            ActionEffectTarget target = a_Targets[i];

            if (target != null && target.GetGameObject() == a_TargetObject)
                return true;
        }

        return false;
    } //private static bool IsTargetAlreadyAdded()

    //대상이 설정된 AreaShape 내부에 포함되는지 확인
    private static bool IsInsideAreaShape(
        ActionEffectTarget a_Target,
        Vector2 a_Center,
        ActionAreaShape a_AreaShape,
        float a_SectorAngle,
        Vector2 a_UseDirection)
    {
        if (a_Target == null || a_Target.HasTarget() == false)
            return false;

        switch (a_AreaShape)
        {
            case ActionAreaShape.Circle:
                return true;

            case ActionAreaShape.Sector:
                return IsInsideSector(
                    a_Target,
                    a_Center,
                    a_SectorAngle,
                    a_UseDirection
                );
        }

        return true;
    } //private static bool IsInsideAreaShape()

    //대상이 시전 방향을 기준으로 Sector 각도 안에 존재하는지 확인
    private static bool IsInsideSector(
        ActionEffectTarget a_Target,
        Vector2 a_Center,
        float a_SectorAngle,
        Vector2 a_UseDirection)
    {
        Vector2 directionToTarget = a_Target.GetPosition() - a_Center;

        if (directionToTarget.sqrMagnitude <= 0.001f)
            return true;

        directionToTarget.Normalize();

        float halfAngle = Mathf.Clamp(
            a_SectorAngle,
            MinimumSectorAngle,
            MaximumSectorAngle
        ) * 0.5f;

        float angle = Vector2.Angle(
            GetValidDirection(a_UseDirection),
            directionToTarget
        );

        return angle <= halfAngle;
    } //private static bool IsInsideSector()

    //0에 가까운 방향을 기본 방향으로 보정하고 정규화
    private static Vector2 GetValidDirection(Vector2 a_Direction)
    {
        if (a_Direction.sqrMagnitude <= 0.001f)
            return Vector2.right;

        return a_Direction.normalized;
    } //private static Vector2 GetValidDirection()

    //Collider에서 CombatTarget을 찾아 ActionEffectTarget으로 변환
    private static ActionEffectTarget CreateTargetFromCollider(
        Collider2D a_Collider,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_Collider == null)
            return null;

        CombatTarget combatTarget = CombatTargetRegistry.GetTarget(a_Collider);

        return GetOrCreateTarget(
            combatTarget,
            a_TargetCache
        );
    } //private static ActionEffectTarget CreateTargetFromCollider()

    //GameObject에서 CombatTarget을 찾아 ActionEffectTarget으로 변환
    private static ActionEffectTarget CreateTargetFromObject(
        GameObject a_Object,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_Object == null)
            return null;

        CombatTarget combatTarget = CombatTargetRegistry.GetTarget(a_Object);

        return GetOrCreateTarget(
            combatTarget,
            a_TargetCache
        );
    } //private static ActionEffectTarget CreateTargetFromObject()

    //CombatTarget에 대응하는 Cached Target을 반환하거나 새 Wrapper 생성
    private static ActionEffectTarget GetOrCreateTarget(
        CombatTarget a_CombatTarget,
        Dictionary<GameObject, ActionEffectTarget> a_TargetCache)
    {
        if (a_CombatTarget == null)
            return null;

        GameObject targetObject = a_CombatTarget.RootObject;

        if (targetObject == null)
            return null;

        if (a_TargetCache != null &&
            a_TargetCache.TryGetValue(
                targetObject,
                out ActionEffectTarget cachedTarget))
        {
            if (cachedTarget != null &&
                cachedTarget.HasTarget() &&
                cachedTarget.GetCombatTarget() == a_CombatTarget)
            {
                return cachedTarget;
            }

            a_TargetCache.Remove(targetObject);
        }

        ActionEffectTarget target = new ActionEffectTarget(a_CombatTarget);

        if (a_TargetCache != null)
            a_TargetCache[targetObject] = target;

        return target;
    } //private static ActionEffectTarget GetOrCreateTarget()

    //Action TargetFilter를 기준으로 대상 관계 유효성 확인
    private static bool IsValidTarget(
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        ActionTargetFilter a_Filter)
    {
        if (a_Context == null || a_Target == null)
            return false;

        CombatTarget targetCombatTarget = a_Target.GetCombatTarget();

        if (targetCombatTarget == null)
            return false;

        CombatTarget casterTarget = GetCasterCombatTarget(a_Context);

        return IsValidCombatTarget(
            casterTarget,
            targetCombatTarget,
            a_Filter
        );
    } //private static bool IsValidTarget()

    //Action Context의 Caster에 대응하는 CombatTarget 반환
    private static CombatTarget GetCasterCombatTarget(ActionUserContext a_Context)
    {
        if (a_Context == null || a_Context.m_CasterObject == null)
            return null;

        return CombatTargetRegistry.GetTarget(a_Context.m_CasterObject);
    } //private static CombatTarget GetCasterCombatTarget()

    //지정된 GameObject를 Action Target으로 검증하고 Wrapper 반환
    public static bool TryResolveTargetObject(
        ActionUserContext a_Context,
        GameObject a_TargetObject,
        ActionTargetFilter a_TargetFilter,
        out ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        a_Target = null;
        a_FailMessage = string.Empty;

        if (a_Context == null)
        {
            a_FailMessage = "액션 실행 정보가 없습니다.";
            return false;
        }

        if (a_TargetObject == null)
        {
            a_FailMessage = "대상으로 지정된 오브젝트가 없습니다.";
            return false;
        }

        CombatTarget combatTarget = CombatTargetRegistry.GetTarget(a_TargetObject);

        if (combatTarget == null)
        {
            a_FailMessage = "지정한 오브젝트는 전투 대상이 아닙니다.";
            return false;
        }

        ActionEffectTarget target = new ActionEffectTarget(combatTarget);

        if (IsValidTarget(
                a_Context,
                target,
                a_TargetFilter) == false)
        {
            a_FailMessage = GetTargetFilterFailMessage(a_TargetFilter);
            return false;
        }

        a_Target = target;

        return true;
    } //public static bool TryResolveTargetObject()

    //TargetFilter 검증 실패 시 사용자에게 전달할 메시지 반환
    private static string GetTargetFilterFailMessage(
        ActionTargetFilter a_TargetFilter)
    {
        switch (a_TargetFilter)
        {
            case ActionTargetFilter.Self:
                return "자신에게만 사용할 수 있는 액션입니다.";

            case ActionTargetFilter.Allies:
                return "아군에게만 사용할 수 있는 액션입니다.";

            case ActionTargetFilter.Enemies:
                return "적에게만 사용할 수 있는 액션입니다.";

            case ActionTargetFilter.Any:
            default:
                return "사용할 수 없는 대상입니다.";
        }
    } //private static string GetTargetFilterFailMessage()

    //Caster와 Target 관계를 검증하고 CombatTarget의 Root Object 반환
    public static bool TryGetValidTargetObject(
        GameObject a_CasterObject,
        GameObject a_TargetObject,
        ActionTargetFilter a_TargetFilter,
        out GameObject a_ResolvedTargetObject)
    {
        a_ResolvedTargetObject = null;

        if (a_CasterObject == null || a_TargetObject == null)
            return false;

        CombatTarget casterTarget = CombatTargetRegistry.GetTarget(a_CasterObject);
        CombatTarget targetCombatTarget = CombatTargetRegistry.GetTarget(a_TargetObject);

        if (IsValidCombatTarget(
                casterTarget,
                targetCombatTarget,
                a_TargetFilter) == false)
        {
            return false;
        }

        a_ResolvedTargetObject = targetCombatTarget.RootObject;

        return a_ResolvedTargetObject != null;
    } //public static bool TryGetValidTargetObject()

    //Caster와 Target의 관계가 지정된 TargetFilter와 일치하는지 확인
    private static bool IsValidCombatTarget(
        CombatTarget a_CasterTarget,
        CombatTarget a_Target,
        ActionTargetFilter a_Filter)
    {
        if (a_Target == null)
            return false;

        if (a_Filter == ActionTargetFilter.Any)
            return true;

        if (a_CasterTarget == null)
            return false;

        switch (a_Filter)
        {
            case ActionTargetFilter.Self:
                return a_CasterTarget == a_Target;

            case ActionTargetFilter.Allies:
                return IsSameTeam(
                    a_CasterTarget,
                    a_Target
                );

            case ActionTargetFilter.Enemies:
                return IsEnemyTeam(
                    a_CasterTarget,
                    a_Target
                );
        }

        return false;
    } //private static bool IsValidCombatTarget()

    //두 CombatTarget이 Neutral이 아닌 같은 Team인지 확인
    private static bool IsSameTeam(
        CombatTarget a_FirstTarget,
        CombatTarget a_SecondTarget)
    {
        CombatTeam firstTeam = a_FirstTarget.CombatTeam;
        CombatTeam secondTeam = a_SecondTarget.CombatTeam;

        if (firstTeam == null || secondTeam == null)
            return false;

        if (firstTeam.TeamType == CombatTeamType.Neutral ||
            secondTeam.TeamType == CombatTeamType.Neutral)
        {
            return false;
        }

        return firstTeam.TeamType == secondTeam.TeamType;
    } //private static bool IsSameTeam()

    //두 CombatTarget이 Neutral이 아닌 서로 다른 Team인지 확인
    private static bool IsEnemyTeam(
        CombatTarget a_FirstTarget,
        CombatTarget a_SecondTarget)
    {
        CombatTeam firstTeam = a_FirstTarget.CombatTeam;
        CombatTeam secondTeam = a_SecondTarget.CombatTeam;

        if (firstTeam == null || secondTeam == null)
            return false;

        if (firstTeam.TeamType == CombatTeamType.Neutral ||
            secondTeam.TeamType == CombatTeamType.Neutral)
        {
            return false;
        }

        return firstTeam.TeamType != secondTeam.TeamType;
    } //private static bool IsEnemyTeam()
} //public static class ActionTargetResolver