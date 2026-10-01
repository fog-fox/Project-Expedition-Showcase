using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ActionStatusController : MonoBehaviour
{
    /*
    대상에게 적용된 Action Status의 지속시간, Tick, Stack과 재적용을 관리하고
    피해, 회복, 방어력 및 이동속도 Modifier를 실제 대상에 적용하는 컨트롤러
    */

    private const float MinimumTickInterval = 0.05f;
    private const float MinimumMoveSpeedMultiplier = 0.01f;
    private const float TickTimeEpsilon = 0.0001f;
    private IActionControlBlockReceiver m_ControlBlockReceiver;

    private readonly struct StatusKey : IEquatable<StatusKey>
    {
        public readonly string m_StatusId;
        public readonly EntityId m_SourceEntityId;

        //Status ID와 Source Entity를 조합한 Status 식별 Key 생성
        public StatusKey(
            string a_StatusId,
            EntityId a_SourceEntityId)
        {
            m_StatusId = a_StatusId;
            m_SourceEntityId = a_SourceEntityId;
        } //public StatusKey()

        //다른 StatusKey와 동일한 Status 및 Source인지 확인
        public bool Equals(StatusKey a_Other)
        {
            return m_StatusId == a_Other.m_StatusId &&
                   m_SourceEntityId == a_Other.m_SourceEntityId;
        } //public bool Equals()

        //Object가 동일한 StatusKey인지 확인
        public override bool Equals(object a_Object)
        {
            return a_Object is StatusKey other && Equals(other);
        } //public override bool Equals()

        //Status ID와 Source Entity를 이용한 Dictionary Hash 생성
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = m_StatusId != null
                    ? m_StatusId.GetHashCode()
                    : 0;

                return (hash * 397) ^ m_SourceEntityId.GetHashCode();
            }
        } //public override int GetHashCode()
    }

    private sealed class ActiveStatus
    {
        public ActionStatusDefinition m_Definition;

        public GameObject m_SourceObject;
        public string m_SourceName;

        public float m_ValuePerTick;
        public DamageType m_DamageType;

        public float m_RemainingDuration;

        public float m_TickInterval;
        public float m_RemainingTickTime;

        public int m_StackCount;

        public float m_NextAllowedApplyTime;

        public float m_ModifierScale;

        public StackResourceReservationHandle m_ResourceHandle;
    }

    private readonly Dictionary<StatusKey, ActiveStatus> m_ActiveStatuses =
        new Dictionary<StatusKey, ActiveStatus>();

    private readonly List<StatusKey> m_UpdateBuffer =
        new List<StatusKey>();

    private readonly List<StatusKey> m_RemoveBuffer =
        new List<StatusKey>();

    private CombatTarget m_CombatTarget;

    private IDamageable m_Damageable;
    private IHealable m_Healable;

    private IDefenseModifierReceiver m_DefenseModifierReceiver;
    private IActionMoveSpeedReceiver m_MoveSpeedReceiver;
    private IActionDetectionReceiver m_DetectionReceiver;

    public event Action<ActionStatusDefinition, int> StatusApplied;
    public event Action<ActionStatusDefinition> StatusRemoved;

    public int ActiveStatusCount => m_ActiveStatuses.Count;

    //Status 적용 대상과 관련 인터페이스 초기 탐색
    private void Awake()
    {
        CacheTargetComponents();
    } //private void Awake()

    //현재 활성 Status의 지속시간과 Tick 상태 갱신
    private void Update()
    {
        if (m_ActiveStatuses.Count == 0)
            return;

        float deltaTime = Mathf.Max(0f, Time.deltaTime);

        m_UpdateBuffer.Clear();
        m_RemoveBuffer.Clear();

        foreach (StatusKey key in m_ActiveStatuses.Keys)
            m_UpdateBuffer.Add(key);

        for (int i = 0; i < m_UpdateBuffer.Count; i++)
        {
            StatusKey key = m_UpdateBuffer[i];

            if (m_ActiveStatuses.TryGetValue(key, out ActiveStatus status) == false)
                continue;

            if (status == null || status.m_Definition == null)
            {
                m_RemoveBuffer.Add(key);
                continue;
            }

            UpdateStatus(
                key,
                status,
                deltaTime
            );
        }

        RemoveBufferedStatuses();
    } //private void Update()

    //대상의 Root Object에서 Status Controller를 찾고 없으면 생성
    public static ActionStatusController GetOrCreate(GameObject a_TargetObject)
    {
        if (a_TargetObject == null)
            return null;

        CombatTarget combatTarget = a_TargetObject.GetComponent<CombatTarget>();

        if (combatTarget == null)
            combatTarget = a_TargetObject.GetComponentInParent<CombatTarget>();

        GameObject rootObject = combatTarget != null
            ? combatTarget.RootObject
            : a_TargetObject;

        if (rootObject == null)
            return null;

        ActionStatusController controller =
            rootObject.GetComponent<ActionStatusController>();

        if (controller == null)
            controller = rootObject.AddComponent<ActionStatusController>();

        return controller;
    } //public static ActionStatusController GetOrCreate()

    //Status Application을 신규 적용하거나 기존 Status에 재적용
    public void ApplyStatus(ActionStatusApplication a_Application)
    {
        ActionStatusDefinition definition = a_Application.m_Definition;

        if (definition == null || string.IsNullOrWhiteSpace(definition.StatusId))
            return;

        CacheTargetComponents();

        StatusKey key = CreateStatusKey(
            definition,
            a_Application.m_SourceObject
        );

        if (m_ActiveStatuses.TryGetValue(key, out ActiveStatus status))
        {
            if (CanReapplyStatus(status, a_Application) == false)
                return;

            RefreshExistingStatus(
                status,
                a_Application
            );
        }
        else
        {
            status = CreateNewStatus(a_Application);

            m_ActiveStatuses.Add(
                key,
                status
            );
        }

        UpdateNextAllowedApplyTime(
            status,
            a_Application
        );

        RefreshDefenseModifier(
            key,
            status
        );

        RefreshMoveSpeedModifier();
        RefreshDetectionModifier();
        RefreshControlBlock();

        StatusApplied?.Invoke(
            definition,
            status.m_StackCount
        );
    } //public void ApplyStatus()

    //현재 활성 Status 중 Control Block 상태가 존재하는지 계산하여 대상에 적용
    private void RefreshControlBlock()
    {
        CacheTargetComponents();

        if (m_ControlBlockReceiver == null)
            return;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status?.m_Definition == null || status.m_Definition.UseControlBlock == false)
                continue;

            m_ControlBlockReceiver.SetActionControlBlocked(true);
            return;
        }

        m_ControlBlockReceiver.SetActionControlBlocked(false);
    } //private void RefreshControlBlock()

    //Status Control Block을 기본 상태로 복원
    private void RestoreControlBlock()
    {
        CacheTargetComponents();
        m_ControlBlockReceiver?.SetActionControlBlocked(false);
    } //private void RestoreControlBlock()

    //지정된 Status Definition이 하나 이상 활성화되어 있는지 확인
    public bool HasStatus(ActionStatusDefinition a_Definition)
    {
        if (a_Definition == null)
            return false;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status != null && status.m_Definition == a_Definition)
                return true;
        }

        return false;
    } //public bool HasStatus()

    //지정된 Status ID가 하나 이상 활성화되어 있는지 확인
    public bool HasStatusId(string a_StatusId)
    {
        if (string.IsNullOrWhiteSpace(a_StatusId))
            return false;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status?.m_Definition == null)
                continue;

            if (status.m_Definition.StatusId == a_StatusId)
                return true;
        }

        return false;
    } //public bool HasStatusId()

    //지정된 Status Tag를 가진 상태가 하나 이상 존재하는지 확인
    public bool HasStatusTag(ActionStatusTag a_Tag)
    {
        if (a_Tag == ActionStatusTag.None)
            return false;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status?.m_Definition == null)
                continue;

            if (status.m_Definition.HasTag(a_Tag))
                return true;
        }

        return false;
    } //public bool HasStatusTag()

    //동일 Status ID를 가진 모든 Source의 Stack 수량 합계 반환
    public int GetTotalStackCount(string a_StatusId)
    {
        if (string.IsNullOrWhiteSpace(a_StatusId))
            return 0;

        int totalStack = 0;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status?.m_Definition == null ||
                status.m_Definition.StatusId != a_StatusId)
            {
                continue;
            }

            totalStack += status.m_StackCount;
        }

        return totalStack;
    } //public int GetTotalStackCount()

    //지정된 Status ID에 해당하는 모든 Source의 Status 제거
    public void RemoveStatus(string a_StatusId)
    {
        if (string.IsNullOrWhiteSpace(a_StatusId))
            return;

        m_RemoveBuffer.Clear();

        foreach (KeyValuePair<StatusKey, ActiveStatus> pair in m_ActiveStatuses)
        {
            ActiveStatus status = pair.Value;

            if (status?.m_Definition == null)
                continue;

            if (status.m_Definition.StatusId == a_StatusId)
                m_RemoveBuffer.Add(pair.Key);
        }

        RemoveBufferedStatuses();
    } //public void RemoveStatus()

    //지정된 Status ID와 Source가 모두 일치하는 Status 제거
    public bool RemoveStatus(
        string a_StatusId,
        GameObject a_SourceObject)
    {
        if (string.IsNullOrWhiteSpace(a_StatusId))
            return false;

        m_RemoveBuffer.Clear();

        foreach (KeyValuePair<StatusKey, ActiveStatus> pair in m_ActiveStatuses)
        {
            ActiveStatus status = pair.Value;

            if (status?.m_Definition == null ||
                status.m_Definition.StatusId != a_StatusId ||
                status.m_SourceObject != a_SourceObject)
            {
                continue;
            }

            m_RemoveBuffer.Add(pair.Key);
        }

        bool removed = m_RemoveBuffer.Count > 0;

        if (removed)
            RemoveBufferedStatuses();

        return removed;
    } //public bool RemoveStatus()

    //현재 대상에게 적용된 모든 Status 제거
    public void ClearAllStatuses()
    {
        if (m_ActiveStatuses.Count == 0)
        {
            RestoreMoveSpeedModifier();
            RestoreDetectionModifier();
            RestoreControlBlock();
            return;
        }

        m_RemoveBuffer.Clear();

        foreach (StatusKey key in m_ActiveStatuses.Keys)
            m_RemoveBuffer.Add(key);

        RemoveBufferedStatuses();
    } //public void ClearAllStatuses()

    //Status Source 정책에 맞는 Dictionary Key 생성
    private StatusKey CreateStatusKey(
        ActionStatusDefinition a_Definition,
        GameObject a_SourceObject)
    {
        EntityId sourceEntityId = EntityId.None;

        if (a_Definition.SourcePolicy == ActionStatusSourcePolicy.PerSource)
        {
            sourceEntityId = a_SourceObject != null
                ? a_SourceObject.GetEntityId()
                : EntityId.None;
        }

        return new StatusKey(
            a_Definition.StatusId,
            sourceEntityId
        );
    } //private StatusKey CreateStatusKey()

    //Application의 Runtime 값을 이용해 새로운 Active Status 생성
    private ActiveStatus CreateNewStatus(ActionStatusApplication a_Application)
    {
        ActionStatusDefinition definition = a_Application.m_Definition;

        int stackCount = Mathf.Clamp(
            a_Application.m_StackAmount,
            1,
            definition.MaxStack
        );

        float tickInterval = Mathf.Max(
            MinimumTickInterval,
            a_Application.m_TickInterval
        );

        return new ActiveStatus
        {
            m_Definition = definition,

            m_SourceObject = a_Application.m_SourceObject,
            m_SourceName = a_Application.m_SourceName,

            m_ValuePerTick = a_Application.m_ValuePerTick,
            m_DamageType = a_Application.m_DamageType,

            m_RemainingDuration = a_Application.m_Duration,

            m_TickInterval = tickInterval,
            m_RemainingTickTime = definition.TickImmediately
                ? 0f
                : tickInterval,

            m_StackCount = stackCount,
            m_NextAllowedApplyTime = 0f,

            m_ModifierScale = Mathf.Max(
                0f,
                a_Application.m_ModifierScale
            ),
            m_ResourceHandle = a_Application.m_ResourceHandle
        };

    } //private ActiveStatus CreateNewStatus()

    //Stack 정책에 맞춰 기존 Status의 Runtime 값과 지속시간 갱신
    private void RefreshExistingStatus(
        ActiveStatus a_Status,
        ActionStatusApplication a_Application)
    {
        RefreshResourceHandle(
            a_Status,
            a_Application.m_ResourceHandle
        );
        if (a_Status?.m_Definition == null)
            return;

        ActionStatusDefinition definition = a_Status.m_Definition;

        if (definition.StackPolicy == ActionStatusStackPolicy.ExtendDuration)
        {
            a_Status.m_RemainingDuration += Mathf.Max(
                0f,
                a_Application.m_Duration
            );

            return;
        }

        a_Status.m_ModifierScale = Mathf.Max(
            0f,
            a_Application.m_ModifierScale
        );

        a_Status.m_SourceObject = a_Application.m_SourceObject;
        a_Status.m_SourceName = a_Application.m_SourceName;

        a_Status.m_ValuePerTick = a_Application.m_ValuePerTick;
        a_Status.m_DamageType = a_Application.m_DamageType;
        a_Status.m_TickInterval = Mathf.Max(
            MinimumTickInterval,
            a_Application.m_TickInterval
        );

        switch (definition.StackPolicy)
        {
            case ActionStatusStackPolicy.RefreshDuration:
                a_Status.m_RemainingDuration = a_Application.m_Duration;
                break;

            case ActionStatusStackPolicy.AddStackAndRefreshDuration:
                a_Status.m_StackCount = Mathf.Clamp(
                    a_Status.m_StackCount + a_Application.m_StackAmount,
                    1,
                    definition.MaxStack
                );

                a_Status.m_RemainingDuration = a_Application.m_Duration;
                break;

            case ActionStatusStackPolicy.Replace:
                a_Status.m_StackCount = Mathf.Clamp(
                    a_Application.m_StackAmount,
                    1,
                    definition.MaxStack
                );

                a_Status.m_RemainingDuration = a_Application.m_Duration;

                a_Status.m_RemainingTickTime = definition.TickImmediately
                    ? 0f
                    : a_Status.m_TickInterval;
                break;
        }
    }

    //하나의 Active Status 지속시간과 Tick Timer 갱신
    private void UpdateStatus(
        StatusKey a_Key,
        ActiveStatus a_Status,
        float a_DeltaTime)
    {
        float remainingDuration = Mathf.Max(0f, a_Status.m_RemainingDuration);
        float activeDeltaTime = Mathf.Min(a_DeltaTime, remainingDuration);

        a_Status.m_RemainingDuration = Mathf.Max(
            0f,
            remainingDuration - a_DeltaTime
        );

        a_Status.m_RemainingTickTime -= activeDeltaTime;

        float tickInterval = Mathf.Max(
            MinimumTickInterval,
            a_Status.m_TickInterval
        );

        while (a_Status.m_RemainingTickTime <= 0f && activeDeltaTime > 0f)
        {
            ExecuteStatusTick(a_Status);

            if (m_ActiveStatuses.TryGetValue(a_Key, out ActiveStatus currentStatus) == false ||
                ReferenceEquals(currentStatus, a_Status) == false)
            {
                return;
            }

            a_Status.m_RemainingTickTime += tickInterval;
        }

        if (a_Status.m_RemainingDuration <= 0f)
            m_RemoveBuffer.Add(a_Key);
    } //private void UpdateStatus()

    //Status Definition의 효과 유형에 맞춰 한 번의 Tick 실행
    private void ExecuteStatusTick(ActiveStatus a_Status)
    {
        if (a_Status?.m_Definition == null)
            return;

        float tickValue = GetStatusTickValue(a_Status);

        if (tickValue <= 0f)
            return;

        switch (a_Status.m_Definition.EffectType)
        {
            case ActionStatusEffectType.Damage:
                ApplyDamageTick(
                    a_Status,
                    tickValue
                );
                break;

            case ActionStatusEffectType.Heal:
                ApplyHealTick(
                    a_Status,
                    tickValue
                );
                break;
        }
    } //private void ExecuteStatusTick()

    //현재 Status Tick 값을 DamageInfo로 변환하여 대상에게 피해 적용
    private void ApplyDamageTick(
        ActiveStatus a_Status,
        float a_Damage)
    {
        CacheTargetComponents();

        if (m_Damageable == null)
            return;

        Vector2 targetPosition = transform.position;

        DamageInfo damageInfo = new DamageInfo(
            a_Status.m_SourceObject,
            gameObject,
            a_Status.m_Definition.StatusId,
            a_Status.m_Definition.DisplayName,
            a_Damage,
            a_Status.m_DamageType,
            targetPosition,
            Vector2.zero
        );

        m_Damageable.TakeDamage(damageInfo);
    } //private void ApplyDamageTick()

    //현재 Status Tick 값을 대상의 회복 시스템에 적용
    private void ApplyHealTick(
        ActiveStatus a_Status,
        float a_HealValue)
    {
        CacheTargetComponents();

        if (m_Healable != null)
        {
            m_Healable.Heal(
                a_HealValue,
                a_Status.m_SourceObject,
                a_Status.m_SourceName
            );

            return;
        }

        if (m_CombatTarget == null)
            return;

        Charic charic = m_CombatTarget.GetCharic();

        if (charic == null ||
            charic.m_CurHp <= 0 ||
            charic.m_CurHp >= charic.m_MaxHp)
        {
            return;
        }

        int healAmount = Mathf.CeilToInt(a_HealValue);

        if (healAmount <= 0)
            return;

        charic.m_CurHp = Mathf.Min(
            charic.m_CurHp + healAmount,
            charic.m_MaxHp
        );
    } //private void ApplyHealTick()

    //Status Stack에 맞는 방어력 Modifier를 갱신
    private void RefreshDefenseModifier(
        StatusKey a_Key,
        ActiveStatus a_Status)
    {
        if (a_Status?.m_Definition == null ||
            a_Status.m_Definition.UseDefenseModifier == false)
        {
            return;
        }

        CacheTargetComponents();

        if (m_DefenseModifierReceiver == null)
            return;

        float modifierValue =
            a_Status.m_Definition.DefenseModifierPerStack *
            Mathf.Max(1, a_Status.m_StackCount) *
            Mathf.Max(0f, a_Status.m_ModifierScale);

        m_DefenseModifierReceiver.SetDefenseModifier(
            CreateDefenseModifierId(a_Key),
            modifierValue
        );
    } //private void RefreshDefenseModifier()

    //Status 제거 시 해당 Status가 등록한 방어력 Modifier 제거
    private void RemoveDefenseModifier(
        StatusKey a_Key,
        ActiveStatus a_Status)
    {
        if (a_Status?.m_Definition == null ||
            a_Status.m_Definition.UseDefenseModifier == false)
        {
            return;
        }

        CacheTargetComponents();

        if (m_DefenseModifierReceiver == null)
            return;

        m_DefenseModifierReceiver.RemoveDefenseModifier(
            CreateDefenseModifierId(a_Key)
        );
    } //private void RemoveDefenseModifier()

    //Status와 Source에 대응하는 고유 Defense Modifier ID 생성
    private static string CreateDefenseModifierId(StatusKey a_Key)
    {
        return $"ActionStatus_{a_Key.m_StatusId}_{a_Key.m_SourceEntityId}";
    } //private static string CreateDefenseModifierId()

    //모든 활성 Status의 이동속도 배율을 계산하여 대상에 적용
    private void RefreshMoveSpeedModifier()
    {
        CacheTargetComponents();

        if (m_MoveSpeedReceiver == null)
            return;

        float totalMultiplier = 1f;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status?.m_Definition == null ||
                status.m_Definition.UseMoveSpeedModifier == false)
            {
                continue;
            }

            float scaledMultiplier =
                ScaleStatusMultiplierFromNeutral(
                    status.m_Definition.MoveSpeedMultiplierPerStack,
                    status.m_ModifierScale
                );

            float statusMultiplier =
                Mathf.Pow(
                    scaledMultiplier,
                    Mathf.Max(1, status.m_StackCount)
                );

            totalMultiplier *= Mathf.Max(
                MinimumMoveSpeedMultiplier,
                statusMultiplier
            );
        }

        m_MoveSpeedReceiver.SetActionMoveSpeedMultiplier(
            Mathf.Max(MinimumMoveSpeedMultiplier, totalMultiplier)
        );
    } //private void RefreshMoveSpeedModifier()

    //모든 활성 Status의 발각도 배율을 계산하여 대상에 적용
    private void RefreshDetectionModifier()
    {
        CacheTargetComponents();

        if (m_DetectionReceiver == null)
            return;

        float totalMultiplier = 1f;

        foreach (ActiveStatus status in m_ActiveStatuses.Values)
        {
            if (status?.m_Definition == null ||
                status.m_Definition.UseDetectionModifier == false)
            {
                continue;
            }

            float scaledMultiplier = ScaleDetectionMultiplierFromNeutral(
                status.m_Definition.DetectionMultiplierPerStack,
                status.m_ModifierScale
            );

            float statusMultiplier = Mathf.Pow(
                scaledMultiplier,
                Mathf.Max(1, status.m_StackCount)
            );

            totalMultiplier *= Mathf.Max(0f, statusMultiplier);

            if (totalMultiplier <= 0f)
                break;
        }

        m_DetectionReceiver.SetActionDetectionMultiplier(
            Mathf.Max(0f, totalMultiplier)
        );
    } //private void RefreshDetectionModifier()

    //발각도 배율의 1 기준 증감 폭에 Runtime 강화 배율 적용
    private static float ScaleDetectionMultiplierFromNeutral(
        float a_Multiplier,
        float a_ModifierScale)
    {
        return Mathf.Max(
            0f,
            1f +
            (a_Multiplier - 1f) *
            Mathf.Max(0f, a_ModifierScale)
        );
    } //private static float ScaleDetectionMultiplierFromNeutral()

    //Status 발각도 Modifier를 기본 배율로 복원
    private void RestoreDetectionModifier()
    {
        CacheTargetComponents();

        m_DetectionReceiver?.SetActionDetectionMultiplier(1f);
    } //private void RestoreDetectionModifier()

    //Status 배율형 Modifier의 1 기준 증감 폭에 Runtime 강화 배율 적용
    private static float ScaleStatusMultiplierFromNeutral(
        float a_Multiplier,
        float a_ModifierScale)
    {
        return Mathf.Max(
            MinimumMoveSpeedMultiplier,
            1f +
            (a_Multiplier - 1f) *
            Mathf.Max(0f, a_ModifierScale)
        );
    } //private static float ScaleStatusMultiplierFromNeutral()

    //Status 이동속도 Modifier를 기본 배율로 복원
    private void RestoreMoveSpeedModifier()
    {
        CacheTargetComponents();

        m_MoveSpeedReceiver?.SetActionMoveSpeedMultiplier(1f);
    } //private void RestoreMoveSpeedModifier()

    //제거 Buffer에 등록된 Status의 Modifier와 Runtime 데이터 제거
    private void RemoveBufferedStatuses()
    {
        bool removedAnyStatus = false;

        for (int i = 0; i < m_RemoveBuffer.Count; i++)
        {
            StatusKey key = m_RemoveBuffer[i];

            if (m_ActiveStatuses.TryGetValue(key, out ActiveStatus status) == false)
                continue;

            RemoveDefenseModifier(
                key,
                status
            );

            bool removed = m_ActiveStatuses.Remove(key);

            if (removed == false)
                continue;

            removedAnyStatus = true;

            if (status?.m_Definition != null)
                StatusRemoved?.Invoke(status.m_Definition);
        }

        m_RemoveBuffer.Clear();

        if (removedAnyStatus)
        {
            RefreshMoveSpeedModifier();
            RefreshDetectionModifier();
            RefreshControlBlock();
        }
    } //private void RemoveBufferedStatuses()

    //Status 적용 대상과 피해, 회복 및 Modifier Receiver 참조 탐색
    private void CacheTargetComponents()
    {
        if (m_CombatTarget == null)
            m_CombatTarget = GetComponent<CombatTarget>();

        if (m_CombatTarget == null)
            m_CombatTarget = GetComponentInParent<CombatTarget>();

        GameObject targetObject = m_CombatTarget != null
            ? m_CombatTarget.RootObject
            : gameObject;

        if (targetObject == null)
            return;

        if (m_Damageable == null)
        {
            if (m_CombatTarget != null)
                m_Damageable = m_CombatTarget.Damageable;

            if (m_Damageable == null)
            {
                m_Damageable =
                    ActionInterfaceUtility.FindInObjectHierarchy<IDamageable>(targetObject);
            }
        }

        if (m_Healable == null)
        {
            m_Healable =
                ActionInterfaceUtility.FindInObjectHierarchy<IHealable>(targetObject);
        }

        if (m_DefenseModifierReceiver == null)
        {
            m_DefenseModifierReceiver =
                ActionInterfaceUtility.FindInObjectHierarchy<IDefenseModifierReceiver>(
                    targetObject
                );
        }

        if (m_MoveSpeedReceiver == null)
        {
            m_MoveSpeedReceiver =
                ActionInterfaceUtility.FindInObjectHierarchy<IActionMoveSpeedReceiver>(
                    targetObject
                );
        }

        if (m_DetectionReceiver == null)
        {
            m_DetectionReceiver =
                ActionInterfaceUtility.FindInObjectHierarchy<IActionDetectionReceiver>(
                    targetObject
                );

            if (m_DetectionReceiver == null)
                m_DetectionReceiver = CombatTargetDetection.GetOrCreate(targetObject);
        }
        if (m_ControlBlockReceiver == null)
        {
            m_ControlBlockReceiver =
                ActionInterfaceUtility.FindInObjectHierarchy<IActionControlBlockReceiver>(
                    targetObject
                );
        }
    } //private void CacheTargetComponents()

    //Application의 재적용 간격을 기준으로 현재 Status 재적용 가능 여부 확인
    private bool CanReapplyStatus(
        ActiveStatus a_Status,
        ActionStatusApplication a_Application)
    {
        if (a_Status == null || a_Application.m_ReapplyInterval <= 0f)
            return true;

        return Time.time >= a_Status.m_NextAllowedApplyTime;
    } //private bool CanReapplyStatus()

    //Application의 재적용 간격을 기준으로 다음 적용 가능 시간 갱신
    private void UpdateNextAllowedApplyTime(
        ActiveStatus a_Status,
        ActionStatusApplication a_Application)
    {
        if (a_Status == null)
            return;

        if (a_Application.m_ReapplyInterval <= 0f)
        {
            a_Status.m_NextAllowedApplyTime = 0f;
            return;
        }

        a_Status.m_NextAllowedApplyTime =
            Time.time + a_Application.m_ReapplyInterval;
    } //private void UpdateNextAllowedApplyTime()

    //대상의 Root Object에서 기존 Status Controller 탐색
    public static ActionStatusController Find(GameObject a_TargetObject)
    {
        if (a_TargetObject == null) return null;

        CombatTarget combatTarget = a_TargetObject.GetComponent<CombatTarget>();

        if (combatTarget == null)
            combatTarget = a_TargetObject.GetComponentInParent<CombatTarget>();

        GameObject rootObject = combatTarget != null
            ? combatTarget.RootObject
            : a_TargetObject;

        if (rootObject == null) return null;

        return rootObject.GetComponent<ActionStatusController>();
    } //public static ActionStatusController Find()

    //지정 Tag를 가진 모든 Status 제거
    public int RemoveStatusesByTag(ActionStatusTag a_Tag)
    {
        if (a_Tag == ActionStatusTag.None || m_ActiveStatuses.Count == 0)
            return 0;

        m_RemoveBuffer.Clear();

        foreach (KeyValuePair<StatusKey, ActiveStatus> pair in m_ActiveStatuses)
        {
            ActiveStatus status = pair.Value;

            if (status?.m_Definition == null ||
                status.m_Definition.HasTag(a_Tag) == false)
            {
                continue;
            }

            m_RemoveBuffer.Add(pair.Key);
        }

        int removeCount = m_RemoveBuffer.Count;

        if (removeCount > 0)
            RemoveBufferedStatuses();

        return removeCount;
    } //public int RemoveStatusesByTag()

    //지정 Tag를 가진 모든 Status의 남은 Tick 값을 즉시 적용한 뒤 제거
    public int TriggerRemainingTicksAndRemoveStatusesByTag(ActionStatusTag a_Tag)
    {
        if (a_Tag == ActionStatusTag.None || m_ActiveStatuses.Count == 0)
            return 0;

        m_UpdateBuffer.Clear();

        foreach (KeyValuePair<StatusKey, ActiveStatus> pair in m_ActiveStatuses)
        {
            ActiveStatus status = pair.Value;

            if (status?.m_Definition == null ||
                status.m_Definition.HasTag(a_Tag) == false)
            {
                continue;
            }

            m_UpdateBuffer.Add(pair.Key);
        }

        if (m_UpdateBuffer.Count == 0)
            return 0;

        int processedCount = 0;

        m_RemoveBuffer.Clear();

        for (int i = 0; i < m_UpdateBuffer.Count; i++)
        {
            StatusKey key = m_UpdateBuffer[i];

            if (m_ActiveStatuses.TryGetValue(key, out ActiveStatus status) == false ||
                status?.m_Definition == null)
            {
                continue;
            }

            ExecuteRemainingStatusTicks(status);
            processedCount++;

            if (m_ActiveStatuses.ContainsKey(key))
                m_RemoveBuffer.Add(key);
        }

        if (m_RemoveBuffer.Count > 0)
            RemoveBufferedStatuses();

        m_UpdateBuffer.Clear();

        return processedCount;
    }

    //Status에 앞으로 남아 있는 모든 Tick 값을 합산하여 즉시 적용
    private void ExecuteRemainingStatusTicks(ActiveStatus a_Status)
    {
        if (a_Status?.m_Definition == null)
            return;

        int remainingTickCount = GetRemainingStatusTickCount(a_Status);

        if (remainingTickCount <= 0)
            return;

        float tickValue = GetStatusTickValue(a_Status);

        if (tickValue <= 0f)
            return;

        float totalValue = tickValue * remainingTickCount;

        switch (a_Status.m_Definition.EffectType)
        {
            case ActionStatusEffectType.Damage:
                ApplyDamageTick(
                    a_Status,
                    totalValue
                );
                break;

            case ActionStatusEffectType.Heal:
                ApplyHealTick(
                    a_Status,
                    totalValue
                );
                break;
        }
    }

    //현재 Status의 남은 시간과 다음 Tick 시간을 기준으로 예정 Tick 수 계산
    private static int GetRemainingStatusTickCount(ActiveStatus a_Status)
    {
        if (a_Status?.m_Definition == null)
            return 0;

        float remainingDuration = Mathf.Max(0f, a_Status.m_RemainingDuration);

        if (remainingDuration <= 0f)
            return 0;

        float tickInterval = Mathf.Max(
            MinimumTickInterval,
            a_Status.m_TickInterval
        );

        float timeUntilNextTick = Mathf.Max(
            0f,
            a_Status.m_RemainingTickTime
        );

        if (timeUntilNextTick > remainingDuration + TickTimeEpsilon)
            return 0;

        float durationAfterFirstTick = Mathf.Max(
            0f,
            remainingDuration - timeUntilNextTick
        );

        return 1 + Mathf.FloorToInt(
            (durationAfterFirstTick + TickTimeEpsilon) / tickInterval
        );
    }

    //현재 Status Stack과 Source Modifier를 반영한 1회 Tick 수치 계산
    private float GetStatusTickValue(ActiveStatus a_Status)
    {
        if (a_Status?.m_Definition == null)
            return 0f;

        float tickValue =
            Mathf.Max(
                0f,
                a_Status.m_ValuePerTick
            );

        int stackCount =
            Mathf.Max(
                1,
                a_Status.m_StackCount
            );

        if (a_Status.m_Definition.ScaleValueByStack)
            tickValue *= stackCount;

        if (a_Status.m_SourceObject == null)
            return tickValue;

        IActionStatusTickModifierProvider modifierProvider =
            ActionInterfaceUtility.FindInObjectHierarchy<IActionStatusTickModifierProvider>(
                a_Status.m_SourceObject
            );

        if (modifierProvider == null)
            return tickValue;

        return Mathf.Max(
            0f,
            modifierProvider.ModifyStatusTickValue(
                a_Status.m_Definition,
                stackCount,
                tickValue
            )
        );
    } //private float GetStatusTickValue()

    //지정 Status ID와 Source Entity가 일치하는 현재 Stack 총합 반환
    public int GetStackCount(
        string a_StatusId,
        GameObject a_SourceObject)
    {
        if (string.IsNullOrWhiteSpace(a_StatusId))
            return 0;

        EntityId sourceEntityId =
            a_SourceObject != null
                ? a_SourceObject.GetEntityId()
                : EntityId.None;

        int totalStackCount = 0;

        foreach (KeyValuePair<StatusKey, ActiveStatus> pair in m_ActiveStatuses)
        {
            ActiveStatus status = pair.Value;

            if (status?.m_Definition == null ||
                status.m_Definition.StatusId != a_StatusId)
            {
                continue;
            }

            if (status.m_Definition.SourcePolicy == ActionStatusSourcePolicy.PerSource &&
                pair.Key.m_SourceEntityId != sourceEntityId)
            {
                continue;
            }

            totalStackCount += Mathf.Max(
                0,
                status.m_StackCount
            );
        }

        return totalStackCount;
    } //public int GetStackCount()

    //다음 피해를 차단하는 활성 Status 하나를 찾아 소비
    public bool TryConsumeDamageGuard()
    {
        if (m_ActiveStatuses.Count == 0)
            return false;

        bool foundGuard = false;
        StatusKey guardKey = default;

        foreach (KeyValuePair<StatusKey, ActiveStatus> pair in m_ActiveStatuses)
        {
            ActiveStatus status = pair.Value;

            if (status?.m_Definition == null || status.m_Definition.UseDamageGuard == false)
                continue;

            guardKey = pair.Key;
            foundGuard = true;
            break;
        }

        if (foundGuard == false)
            return false;

        if (m_RemoveBuffer.Contains(guardKey) == false)
            m_RemoveBuffer.Add(guardKey);

        RemoveBufferedStatuses();

        return true;
    } //public bool TryConsumeDamageGuard()

    //기존 Status가 Resource Handle을 보유하지 않을 때 신규 Handle 인계
    private static void RefreshResourceHandle(
        ActiveStatus a_Status,
        StackResourceReservationHandle a_NewHandle)
    {
        if (a_Status == null || a_NewHandle == null)
            return;

        if (a_Status.m_ResourceHandle != null && a_Status.m_ResourceHandle.IsValid)
        {
            a_NewHandle.ReleaseImmediately();
            return;
        }

        a_Status.m_ResourceHandle = a_NewHandle;
    } //private static void RefreshResourceHandle()

    //Status가 보유한 Stack Resource를 Recovery 상태로 전환
    private static void ReleaseResourceHandle(ActiveStatus a_Status)
    {
        if (a_Status?.m_ResourceHandle == null)
            return;

        if (a_Status.m_ResourceHandle.IsValid)
            a_Status.m_ResourceHandle.StartRecovery();

        a_Status.m_ResourceHandle = null;
    } //private static void ReleaseResourceHandle()

    //컴포넌트 비활성화 시 모든 Status와 Modifier 제거
    private void OnDisable()
    {
        ClearAllStatuses();
    } //private void OnDisable()

    //컴포넌트 제거 시 남아 있는 모든 Status와 Modifier 정리
    private void OnDestroy()
    {
        ClearAllStatuses();
    } //private void OnDestroy()
} //public class ActionStatusController