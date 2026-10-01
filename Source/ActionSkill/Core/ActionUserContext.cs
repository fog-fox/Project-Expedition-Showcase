using System;
using System.Collections.Generic;
using UnityEngine;

public class ActionUserContext
{
    /*
    Action 실행에 필요한 Caster 정보와 사용 방향, 대상,
    Source 정보 및 Runtime Asset 참조를 저장하는 실행 Context 클래스
    */

    private const float MinimumDirectionSqrMagnitude = 0.001f;

    public readonly MonoBehaviour m_RoutineOwner;

    public readonly GameObject m_CasterObject;
    public readonly Transform m_CasterTransform;
    public readonly Rigidbody2D m_CasterRigidbody;

    public readonly Player m_CasterPlayer;

    public readonly Vector2 m_UseDirection;
    public readonly Vector2 m_MouseWorldPosition;

    public readonly GameObject m_HitObject;

    public readonly string m_SourceName;

    public readonly Action<bool> m_SetControlLock;

    private readonly CharicRuntimeAssetProvider m_AssetProvider;

    private readonly float m_InheritedHealMultiplier;
    private readonly float m_InheritedBuffMultiplier;

    //기존 Action 생성 경로에서 기본 회복 및 Buff 배율 1을 사용하여 Context 초기화
    public ActionUserContext(
        MonoBehaviour a_RoutineOwner,
        GameObject a_CasterObject,
        Transform a_CasterTransform,
        Rigidbody2D a_CasterRigidbody,
        Player a_CasterPlayer,
        Vector2 a_UseDirection,
        Vector2 a_MouseWorldPosition,
        GameObject a_HitObject,
        string a_SourceName,
        Action<bool> a_SetControlLock,
        CharicRuntimeAssetProvider a_AssetProvider)
        : this(
            a_RoutineOwner,
            a_CasterObject,
            a_CasterTransform,
            a_CasterRigidbody,
            a_CasterPlayer,
            a_UseDirection,
            a_MouseWorldPosition,
            a_HitObject,
            a_SourceName,
            a_SetControlLock,
            a_AssetProvider,
            1f,
            1f)
    {
    } //public ActionUserContext()

    //Caster GameObject 반환
    public GameObject GetCasterObject()
    {
        return m_CasterObject;
    } //public GameObject GetCasterObject()

    //Caster Transform 반환
    public Transform GetCasterTransform()
    {
        return m_CasterTransform;
    } //public Transform GetCasterTransform()

    //Caster Rigidbody2D 반환
    public Rigidbody2D GetCasterRigidbody()
    {
        return m_CasterRigidbody;
    } //public Rigidbody2D GetCasterRigidbody()

    //Caster Player 반환
    public Player GetCasterPlayer()
    {
        return m_CasterPlayer;
    } //public Player GetCasterPlayer()

    //정규화된 Action 사용 방향 반환
    public Vector2 GetUseDirection()
    {
        return m_UseDirection;
    } //public Vector2 GetUseDirection()

    //Caster의 현재 World Position 반환
    public Vector2 GetCasterPosition()
    {
        if (m_CasterTransform == null) return Vector2.zero;

        return m_CasterTransform.position;
    } //public Vector2 GetCasterPosition()

    //현재 Action의 Hit Object 반환
    public GameObject GetHitObject()
    {
        return m_HitObject;
    } //public GameObject GetHitObject()

    //현재 Action의 Source 이름 반환
    public string GetSourceName()
    {
        return m_SourceName;
    } //public string GetSourceName()

    //Item 사용 Context에서 ActionUserContext 생성
    public static ActionUserContext FromItemUseContext(ItemUseContext a_Context, string a_SourceName)
    {
        if (a_Context?.m_SkillContext == null) return null;

        return a_Context.m_SkillContext.CreateActionUserContext(a_SourceName, a_Context.m_Player);
    } //public static ActionUserContext FromItemUseContext()

    //Skill Context에서 ActionUserContext 생성
    public static ActionUserContext FromSkillContext(SkillContext a_Context, string a_SourceName)
    {
        if (a_Context == null) return null;

        Player casterPlayer = FindCasterPlayer(a_Context);

        return a_Context.CreateActionUserContext(a_SourceName, casterPlayer);
    } //public static ActionUserContext FromSkillContext()

    //Action 대상 탐색에 사용할 LayerMask 반환
    public LayerMask GetTargetLayerMask()
    {
        if (m_AssetProvider == null) return default;

        return m_AssetProvider.GetTargetLayerMask();
    } //public LayerMask GetTargetLayerMask()

    //Projectile 충돌 판정에 사용할 Block LayerMask 반환
    public LayerMask GetBlockLayerMask()
    {
        if (m_AssetProvider == null) return default;

        return m_AssetProvider.GetProjectileBlockLayerMask();
    } //public LayerMask GetBlockLayerMask()

    //Caster의 Runtime Asset Provider 반환
    public CharicRuntimeAssetProvider GetAssetProvider()
    {
        return m_AssetProvider;
    } //public CharicRuntimeAssetProvider GetAssetProvider()

    //Caster Player에 연결된 Charic 데이터 반환
    public Charic GetCasterCharic()
    {
        if (m_CasterPlayer == null) return null;

        return m_CasterPlayer.GetCharicData();
    } //public Charic GetCasterCharic()

    //Skill Context의 Caster Object에서 Player 탐색
    private static Player FindCasterPlayer(SkillContext a_Context)
    {
        if (a_Context == null) return null;

        GameObject casterObject = a_Context.GetCasterObject();

        if (casterObject == null) return null;

        return casterObject.GetComponentInParent<Player>();
    } //private static Player FindCasterPlayer()

    //0에 가까운 방향을 기본 방향으로 보정하고 정규화
    private static Vector2 GetValidDirection(Vector2 a_Direction)
    {
        if (a_Direction.sqrMagnitude <= MinimumDirectionSqrMagnitude) return Vector2.right;

        return a_Direction.normalized;
    } //private static Vector2 GetValidDirection()

    //상위 Action에서 상속된 회복 강화 배율 반환
    public float GetInheritedHealMultiplier()
    {
        return Mathf.Max(0f, m_InheritedHealMultiplier);
    } //public float GetInheritedHealMultiplier()

    //상위 Action에서 상속된 Buff 강화 배율 반환
    public float GetInheritedBuffMultiplier()
    {
        return Mathf.Max(0f, m_InheritedBuffMultiplier);
    } //public float GetInheritedBuffMultiplier()

    //상위 Action의 회복 및 Buff 강화 배율을 포함하여 Context 초기화
    public ActionUserContext(
        MonoBehaviour a_RoutineOwner,
        GameObject a_CasterObject,
        Transform a_CasterTransform,
        Rigidbody2D a_CasterRigidbody,
        Player a_CasterPlayer,
        Vector2 a_UseDirection,
        Vector2 a_MouseWorldPosition,
        GameObject a_HitObject,
        string a_SourceName,
        Action<bool> a_SetControlLock,
        CharicRuntimeAssetProvider a_AssetProvider,
        float a_InheritedHealMultiplier = 1f,
        float a_InheritedBuffMultiplier = 1f)
    {
        m_RoutineOwner = a_RoutineOwner;
        m_CasterObject = a_CasterObject;
        m_CasterTransform = a_CasterTransform != null ? a_CasterTransform : a_CasterObject != null ? a_CasterObject.transform : null;
        m_CasterRigidbody = a_CasterRigidbody;
        m_CasterPlayer = a_CasterPlayer;
        m_UseDirection = GetValidDirection(a_UseDirection);
        m_MouseWorldPosition = a_MouseWorldPosition;
        m_HitObject = a_HitObject;
        m_SourceName = a_SourceName;
        m_SetControlLock = a_SetControlLock;
        m_AssetProvider = a_AssetProvider;
        m_InheritedHealMultiplier = Mathf.Max(0f, a_InheritedHealMultiplier);
        m_InheritedBuffMultiplier = Mathf.Max(0f, a_InheritedBuffMultiplier);
    } //public ActionUserContext()

    private readonly HashSet<ActionEffectApplicationKey> m_AppliedEffectTargets = new HashSet<ActionEffectApplicationKey>();

    private readonly struct ActionEffectApplicationKey : IEquatable<ActionEffectApplicationKey>
    {
        /*
        하나의 Action 실행 안에서 특정 Effect가 특정 대상에게
        이미 적용되었는지 구분하기 위한 Runtime Key
        */

        private readonly int m_ActionInstanceId;
        private readonly int m_TargetInstanceId;
        private readonly string m_EffectId;

        //Effect 적용 대상 Key 초기화
        public ActionEffectApplicationKey(
            ActionDefinition a_ActionDefinition,
            string a_EffectId,
            GameObject a_TargetObject)
        {
            m_ActionInstanceId = a_ActionDefinition != null ? a_ActionDefinition.GetInstanceID() : 0;
            m_TargetInstanceId = a_TargetObject != null ? a_TargetObject.GetInstanceID() : 0;
            m_EffectId = a_EffectId ?? string.Empty;
        } //public ActionEffectApplicationKey()

        //다른 Effect 적용 Key와 동일 여부 확인
        public bool Equals(ActionEffectApplicationKey a_Other)
        {
            return m_ActionInstanceId == a_Other.m_ActionInstanceId &&
                   m_TargetInstanceId == a_Other.m_TargetInstanceId &&
                   m_EffectId == a_Other.m_EffectId;
        } //public bool Equals()

        //Object 기준 동일 여부 확인
        public override bool Equals(object a_Object)
        {
            return a_Object is ActionEffectApplicationKey other && Equals(other);
        } //public override bool Equals()

        //HashSet 비교에 사용할 HashCode 반환
        public override int GetHashCode()
        {
            return HashCode.Combine(
                m_ActionInstanceId,
                m_TargetInstanceId,
                m_EffectId
            );
        } //public override int GetHashCode()
    }

    //현재 Action 실행에서 지정 Effect와 대상 조합을 최초 적용으로 등록
    public bool TryRegisterEffectTarget(
        ActionDefinition a_ActionDefinition,
        string a_EffectId,
        GameObject a_TargetObject)
    {
        if (a_ActionDefinition == null || a_TargetObject == null)
            return false;

        ActionEffectApplicationKey key =
            new ActionEffectApplicationKey(
                a_ActionDefinition,
                a_EffectId,
                a_TargetObject
            );

        return m_AppliedEffectTargets.Add(key);
    } //public bool TryRegisterEffectTarget()
} //public class ActionUserContext