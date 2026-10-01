using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class ActionEffectInfo
{
    /*
    Action Effect 하나의 원본 설정을 저장하고
    Runtime Effect 생성과 실제 Effect 실행기로 연결하는 데이터 클래스
    */

    [Header("Identity")]
    [Tooltip("특정 Effect를 구분하거나 장비 보정 대상으로 지정할 때 사용하는 고유 ID.")]
    [SerializeField] private string m_EffectId;

    [Header("Effect")]
    [SerializeField] private ActionEffectType m_EffectType = ActionEffectType.Heal;
    [SerializeField] private ActionApplyMode m_ApplyMode = ActionApplyMode.Instant;

    [Header("Value")]
    [SerializeField] private float m_ValuePerApply = 10f;
    [SerializeField] private DamageType m_DamageType = DamageType.Physical;
    [SerializeField] private float m_KnockbackPower;

    [Header("Critical")]
    [Tooltip("활성화하면 이 Damage Effect가 치명타 판정을 수행합니다.")]
    [SerializeField] private bool m_CanCritical;

    [SerializeField, Range(0f, 1f)] private float m_CriticalChance;

    [SerializeField, Min(1f)] private float m_CriticalDamageMultiplier = 1.5f;

    [Header("Use Condition")]
    [Tooltip("활성화하면 대상의 현재 HP가 회복 가능한 최대 HP 이상일 때 Effect 적용을 거부합니다.")]
    [SerializeField] private bool m_RequireMissingRecoverableHealth;

    [Header("Over Time")]
    [SerializeField] private float m_Duration = 5f;
    [SerializeField] private float m_TickInterval = 1f;

    [Header("Movement")]
    [SerializeField] private ActionDashDirectionMode m_DashDirectionMode = ActionDashDirectionMode.UseDirection;
    [SerializeField] private float m_MoveDistance = 4f;
    [SerializeField] private float m_MoveDuration = 0.2f;
    [SerializeField] private float m_StopDistance = 1f;

    [Header("Pull")]
    [SerializeField] private float m_PullForce = 35f;
    [SerializeField] private float m_PullMaxSpeed = 12f;

    [Header("Push")]
    [SerializeField] private ActionPushDirectionMode m_PushDirectionMode = ActionPushDirectionMode.ImpactCenter;

    [Header("Attached Status")]
    [Tooltip(
        "상태의 지속시간, Tick, 피해 및 스탯 설정을 정의하는 Status Definition."
    )]
    [SerializeField] private ActionStatusDefinition m_StatusDefinition;

    [SerializeField, Min(1)] private int m_StatusStackAmount = 1;

    [Tooltip("활성화 시 Action Effect와 별도의 상태 지속시간 사용.")]
    [SerializeField] private bool m_UseSeparateStatusDuration;

    [SerializeField, Min(0.01f)] private float m_StatusDuration = 4f;

    [Tooltip("활성화 시 Action Effect와 별도의 상태 Tick 간격 사용.")]
    [SerializeField] private bool m_UseSeparateStatusTickInterval;

    [SerializeField, Min(0.05f)] private float m_StatusTickInterval = 1f;

    [Tooltip(
        "같은 시전자가 같은 대상에게 상태를 다시 적용할 수 있는 최소 간격.\n" +
        "0이면 재적용 간격을 제한하지 않음."
    )]
    [SerializeField, Min(0f)] private float m_StatusReapplyInterval;

    [Header("Status Consume")]
    [Tooltip("처리할 Status의 Tag.")]
    [SerializeField] private ActionStatusTag m_TargetStatusTag = ActionStatusTag.None;

    [Tooltip("탐색된 Status의 처리 방식.")]
    [SerializeField] private ActionStatusConsumeMode m_StatusConsumeMode = ActionStatusConsumeMode.Remove;

    [Header("Stack Resource")]
    [Tooltip("활성화하면 이 Effect가 시전자의 Pending Stack Resource Reservation을 인계받아 효과 수명 동안 유지.")]
    [SerializeField] private bool m_ClaimStackResourceReservation;

    [Header("Shield")]
    [SerializeField]
    private ShieldStackPolicy m_ShieldStackPolicy = ShieldStackPolicy.ReplaceSameSource;

    [Tooltip("활성화 시 같은 Effect ID의 보호막이 존재하면 다시 적용하지 않음.")]
    [SerializeField] private bool m_BlockIfSameShieldActive;

    [Header("Next Basic Attack")]
    [Tooltip("다음 기본공격에 적용되는 피해 배율.")]
    [SerializeField, Min(0f)] private float m_NextBasicAttackDamageMultiplier = 1.5f;

    [Tooltip("다음 기본공격에 적용되는 범위 배율.")]
    [SerializeField, Min(0f)] private float m_NextBasicAttackRadiusMultiplier = 1.25f;

    [Header("Deploy Wall")]
    [SerializeField] private MetalWall m_WallPrefab;

    [Tooltip("시전자 중심에서 벽이 생성되는 전방 거리.")]
    [SerializeField, Min(0f)] private float m_WallSpawnDistance = 1.5f;

    [SerializeField, Min(1)] private int m_WallMaxHealth = 300;
    [SerializeField, Min(0f)] private float m_WallDefense;

    [Tooltip("벽 생성 위치가 다른 충돌체와 겹치는지 검사할 때 사용하는 LayerMask.")]
    [SerializeField] private LayerMask m_WallPlacementBlockMask;

    [Header("Triggered Effect")]
    [SerializeField] private ActionTriggerDefinition m_TriggerDefinition;

    [Header("Reactive Mine")]
    [Tooltip("설치할 반응형 지뢰 Prefab.")]
    [SerializeField] private ReactiveMine m_ReactiveMinePrefab;

    [Tooltip("지뢰 발동 시 대상에게 실행할 Reaction Action.")]
    [SerializeField] private ActionDefinition m_ReactiveMineReactionAction;

    [Tooltip("동시에 유지할 수 있는 최대 설치 지뢰 수.")]
    [FormerlySerializedAs("m_ReactiveMineMaximumCount")]
    [SerializeField, Min(1)] private int m_ReactiveMineMaximumActiveCount = 3;

    [Header("Effect Lifecycle")]
    [SerializeField] private bool m_UseLifecycleEvent;

    [SerializeField]
    private ActionEffectLifecycleType m_LifecycleType =
        ActionEffectLifecycleType.Shield;

    [SerializeField]
    private ActionEffectLifecycleTag m_LifecycleTags =
        ActionEffectLifecycleTag.None;

    [Header("Application")]
    [SerializeField] private ActionEffectTargetApplicationMode m_TargetApplicationMode = ActionEffectTargetApplicationMode.EveryImpact;


    [Header("Deploy Decoy")]
    [SerializeField] private CombatDecoy m_DecoyPrefab;

    public string EffectId => m_EffectId;

    public ActionEffectType EffectType => m_EffectType;
    public ActionApplyMode ApplyMode => m_ApplyMode;

    public float ValuePerApply => m_ValuePerApply;
    public DamageType DamageType => m_DamageType;

    public bool CanCritical => m_CanCritical;
    public float CriticalChance => Mathf.Clamp01(m_CriticalChance);
    public float CriticalDamageMultiplier => Mathf.Max(1f, m_CriticalDamageMultiplier);

    public bool RequireMissingRecoverableHealth => m_RequireMissingRecoverableHealth;

    public float KnockbackPower => Mathf.Max(0f, m_KnockbackPower);

    public float Duration => Mathf.Max(0f, m_Duration);
    public float TickInterval => Mathf.Max(0.05f, m_TickInterval);

    public float MoveDistance => Mathf.Max(0f, m_MoveDistance);
    public float MoveDuration => Mathf.Max(0.01f, m_MoveDuration);
    public float StopDistance => Mathf.Max(0.05f, m_StopDistance);

    public ActionDashDirectionMode DashDirectionMode => m_DashDirectionMode;

    public float PullForce => Mathf.Max(0f, m_PullForce);
    public float PullMaxSpeed => Mathf.Max(0.1f, m_PullMaxSpeed);

    public ActionStatusDefinition StatusDefinition => m_StatusDefinition;
    public int StatusStackAmount => Mathf.Max(1, m_StatusStackAmount);

    public float StatusDuration =>
        m_UseSeparateStatusDuration ? Mathf.Max(0.01f, m_StatusDuration) : Duration;

    public float StatusTickInterval =>
        m_UseSeparateStatusTickInterval
            ? Mathf.Max(0.05f, m_StatusTickInterval)
            : TickInterval;
    public ActionStatusTag TargetStatusTag => m_TargetStatusTag;
    public ActionStatusConsumeMode StatusConsumeMode => m_StatusConsumeMode;

    public float StatusReapplyInterval => Mathf.Max(0f, m_StatusReapplyInterval);

    public ShieldStackPolicy ShieldStackPolicy => m_ShieldStackPolicy;
    public bool BlockIfSameShieldActive => m_BlockIfSameShieldActive;

    public float NextBasicAttackDamageMultiplier =>
        Mathf.Max(0f, m_NextBasicAttackDamageMultiplier);

    public float NextBasicAttackRadiusMultiplier =>
        Mathf.Max(0f, m_NextBasicAttackRadiusMultiplier);

    public MetalWall WallPrefab => m_WallPrefab;
    public float WallSpawnDistance => Mathf.Max(0f, m_WallSpawnDistance);
    public int WallMaxHealth => Mathf.Max(1, m_WallMaxHealth);
    public float WallDefense => Mathf.Max(0f, m_WallDefense);
    public LayerMask WallPlacementBlockMask => m_WallPlacementBlockMask;

    public ActionTriggerDefinition TriggerDefinition => m_TriggerDefinition;

    public ReactiveMine ReactiveMinePrefab => m_ReactiveMinePrefab;
    public ActionDefinition ReactiveMineReactionAction => m_ReactiveMineReactionAction;
    public int ReactiveMineMaximumActiveCount => Mathf.Max(1, m_ReactiveMineMaximumActiveCount);

    public bool UseLifecycleEvent => m_UseLifecycleEvent;
    public ActionEffectLifecycleType LifecycleType => m_LifecycleType;
    public ActionEffectLifecycleTag LifecycleTags => m_LifecycleTags;

    public ActionEffectTargetApplicationMode TargetApplicationMode => m_TargetApplicationMode;
    public ActionPushDirectionMode PushDirectionMode => m_PushDirectionMode;
    public CombatDecoy DecoyPrefab => m_DecoyPrefab;
    public bool ClaimStackResourceReservation => m_ClaimStackResourceReservation;
    //Effect가 지속적으로 반복 적용되는 방식인지 확인
    public bool IsOverTime()
    {
        return m_ApplyMode == ActionApplyMode.OverTime;
    } //public bool IsOverTime()

    //Runtime에서 사용할 ApplyStatus Effect 설정 생성
    public static ActionEffectInfo CreateRuntimeStatusEffect(
        string a_EffectId,
        ActionApplyMode a_ApplyMode,
        float a_Duration,
        float a_TickInterval,
        ActionStatusDefinition a_StatusDefinition,
        int a_StatusStackAmount,
        float a_StatusDuration,
        float a_StatusReapplyInterval)
    {
        if (a_StatusDefinition == null)
            return null;

        ActionEffectInfo effect = new ActionEffectInfo
        {
            m_EffectId = a_EffectId,
            m_EffectType = ActionEffectType.ApplyStatus,
            m_ApplyMode = a_ApplyMode,
            m_ValuePerApply = 0f,

            m_Duration = Mathf.Max(0.05f, a_Duration),
            m_TickInterval = Mathf.Max(0.05f, a_TickInterval),

            m_StatusDefinition = a_StatusDefinition,
            m_StatusStackAmount = Mathf.Max(1, a_StatusStackAmount),

            m_UseSeparateStatusDuration = true,
            m_StatusDuration = Mathf.Max(0.01f, a_StatusDuration),

            m_UseSeparateStatusTickInterval = true,
            m_StatusTickInterval = Mathf.Max(0.05f, a_TickInterval),

            m_StatusReapplyInterval = Mathf.Max(0f, a_StatusReapplyInterval)
        };

        return effect;
    } //public static ActionEffectInfo CreateRuntimeStatusEffect()

    //현재 Effect의 원본 설정으로 대상에게 효과 적용
    public void Apply(
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition)
    {
        Apply(
            a_Context,
            a_Target,
            a_ImpactPosition,
            null
        );
    } //public void Apply()

    //Runtime Effect 데이터를 사용하여 대상에게 효과 적용
    public void Apply(
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ActionEffectExecutor.Apply(
            this,
            a_Context,
            a_Target,
            a_ImpactPosition,
            a_RuntimeEffect
        );
    } //public void Apply()

    //현재 Effect를 지정된 대상에게 적용할 수 있는지 확인
    public bool CanApply(
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        return ActionEffectExecutor.CanApply(
            this,
            a_Context,
            a_Target,
            out a_FailMessage
        );
    } //public bool CanApply()
} //public class ActionEffectInfo