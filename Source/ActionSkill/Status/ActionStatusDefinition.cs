using UnityEngine;

[CreateAssetMenu(
    menuName = "Last Expedition/Action/Status Definition",
    fileName = "New Status Definition"
)]
public class ActionStatusDefinition : ScriptableObject
{
    /*
    Action Status의 식별 정보와 Stack, Source, Lifetime,
    주기 효과 및 방어력, 이동속도, 발각도 Modifier 설정을 정의하는 ScriptableObject
    */

    private const float MinimumDuration = 0.01f;
    private const float MinimumTickInterval = 0.05f;
    private const float MinimumMoveSpeedMultiplier = 0.01f;

    [Header("Identity")]
    [SerializeField] private string m_StatusId = "NewStatus";
    [SerializeField] private string m_DisplayName = "새로운 상태";

    [TextArea]
    [SerializeField] private string m_Description;

    [Header("Classification")]
    [SerializeField] private ActionStatusTag m_Tags = ActionStatusTag.None;
    [SerializeField] private ActionStatusEffectType m_EffectType = ActionStatusEffectType.None;

    [Header("Stack")]
    [SerializeField] private ActionStatusStackPolicy m_StackPolicy = ActionStatusStackPolicy.RefreshDuration;
    [SerializeField, Min(1)] private int m_MaxStack = 1;

    [Tooltip(
        "활성화하면 Tick 효과의 수치가 현재 Stack 수에 비례.\n" +
        "예: Tick 수치 2, Stack 5라면 최종 수치는 10."
    )]
    [SerializeField] private bool m_ScaleValueByStack;

    [Header("Source")]
    [Tooltip("Status를 Source별로 별도 관리할지 모든 Source가 공유할지 결정.")]
    [SerializeField] private ActionStatusSourcePolicy m_SourcePolicy = ActionStatusSourcePolicy.Shared;

    [Header("Runtime Value Policy")]
    [Tooltip(
        "활성화하면 Status의 지속시간, Tick 수치, Damage Type, Tick 간격, " +
        "재적용 간격을 개별 Action Effect가 아닌 이 Definition의 값으로 통일."
    )]
    [SerializeField] private bool m_UseDefinitionRuntimeValues;

    [Header("Lifetime")]
    [Tooltip("Status가 유지되는 기본 시간.")]
    [SerializeField, Min(MinimumDuration)] private float m_Duration = 5f;

    [Tooltip(
        "동일 Status를 다시 적용할 수 있는 최소 간격.\n" +
        "Source 구분 여부는 Source Policy를 따르며 0이면 제한하지 않음."
    )]
    [SerializeField, Min(0f)] private float m_ReapplyInterval;

    [Header("Periodic Effect")]
    [Tooltip("Damage 또는 Heal Status가 한 번의 Tick마다 적용하는 기본 수치.")]
    [SerializeField, Min(0f)] private float m_ValuePerTick;

    [SerializeField] private DamageType m_DamageType = DamageType.Physical;

    [Tooltip("주기 효과가 반복되는 기본 간격.")]
    [SerializeField, Min(MinimumTickInterval)] private float m_TickInterval = 1f;

    [Tooltip("Status가 처음 적용된 직후 첫 Damage 또는 Heal Tick 실행.")]
    [SerializeField] private bool m_TickImmediately = true;

    [Header("Defense Modifier")]
    [Tooltip("활성화하면 Status가 유지되는 동안 대상의 방어력을 변경.")]
    [SerializeField] private bool m_UseDefenseModifier;

    [Header("Control Block")]
    [Tooltip("활성화하면 Status가 유지되는 동안 대상의 일반적인 전투 행동을 차단.")]
    [SerializeField] private bool m_UseControlBlock;

    [Tooltip(
        "Stack 1개당 적용되는 방어력 보정값.\n" +
        "방어력 감소는 음수 값 사용."
    )]
    [SerializeField] private float m_DefenseModifierPerStack;

    [Header("Move Speed Modifier")]
    [Tooltip("활성화하면 Status가 유지되는 동안 대상의 이동속도를 변경.")]
    [SerializeField] private bool m_UseMoveSpeedModifier;

    [Tooltip(
        "Stack 1개당 적용되는 이동속도 배율.\n" +
        "0.8은 20% 감소, 1.2는 20% 증가.\n" +
        "여러 Stack은 배율을 거듭제곱하여 계산."
    )]
    [SerializeField, Min(MinimumMoveSpeedMultiplier)] private float m_MoveSpeedMultiplierPerStack = 1f;

    [Header("Detection Modifier")]
    [Tooltip("활성화하면 Status가 유지되는 동안 대상의 발각도를 변경.")]
    [SerializeField] private bool m_UseDetectionModifier;

    [Tooltip(
        "Stack 1개당 적용되는 발각도 배율.\n" +
        "0은 완전 은폐, 0.5는 발각도 50%, 2는 발각도 200%."
    )]
    [SerializeField, Min(0f)] private float m_DetectionMultiplierPerStack = 1f;

    [Header("Damage Guard")]
    [Tooltip("활성화하면 Status가 유지되는 동안 다음 유효 피해 1회를 완전히 막고 Status를 소비.")]
    [SerializeField] private bool m_UseDamageGuard;


    public string StatusId =>
        string.IsNullOrWhiteSpace(m_StatusId) ? name : m_StatusId;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(m_DisplayName) ? name : m_DisplayName;

    public string Description => m_Description;

    public ActionStatusTag Tags => m_Tags;
    public ActionStatusEffectType EffectType => m_EffectType;

    public ActionStatusStackPolicy StackPolicy => m_StackPolicy;
    public int MaxStack => Mathf.Max(1, m_MaxStack);
    public bool ScaleValueByStack => m_ScaleValueByStack;

    public ActionStatusSourcePolicy SourcePolicy => m_SourcePolicy;

    public bool UseDefinitionRuntimeValues => m_UseDefinitionRuntimeValues;

    public float Duration => Mathf.Max(MinimumDuration, m_Duration);
    public float ReapplyInterval => Mathf.Max(0f, m_ReapplyInterval);

    public float ValuePerTick => Mathf.Max(0f, m_ValuePerTick);
    public DamageType DamageType => m_DamageType;
    public float TickInterval => Mathf.Max(MinimumTickInterval, m_TickInterval);
    public bool TickImmediately => m_TickImmediately;

    public bool UseDefenseModifier => m_UseDefenseModifier;
    public float DefenseModifierPerStack => m_DefenseModifierPerStack;

    public bool UseMoveSpeedModifier => m_UseMoveSpeedModifier;

    public float MoveSpeedMultiplierPerStack =>
        Mathf.Max(MinimumMoveSpeedMultiplier, m_MoveSpeedMultiplierPerStack);

    public bool UseDetectionModifier => m_UseDetectionModifier;
    public float DetectionMultiplierPerStack => Mathf.Max(0f, m_DetectionMultiplierPerStack);

    public bool UseControlBlock => m_UseControlBlock;

    public bool UseDamageGuard => m_UseDamageGuard;

    //지정된 Status Tag가 현재 Definition에 모두 포함되어 있는지 확인
    public bool HasTag(ActionStatusTag a_Tag)
    {
        if (a_Tag == ActionStatusTag.None)
            return false;

        return (m_Tags & a_Tag) == a_Tag;
    } //public bool HasTag()

#if UNITY_EDITOR
    //Inspector에서 입력된 Status 설정값을 유효 범위로 보정
    private void OnValidate()
    {
        m_MaxStack = Mathf.Max(1, m_MaxStack);

        m_Duration = Mathf.Max(MinimumDuration, m_Duration);
        m_ReapplyInterval = Mathf.Max(0f, m_ReapplyInterval);

        m_ValuePerTick = Mathf.Max(0f, m_ValuePerTick);
        m_TickInterval = Mathf.Max(MinimumTickInterval, m_TickInterval);

        m_MoveSpeedMultiplierPerStack = Mathf.Max(
            MinimumMoveSpeedMultiplier,
            m_MoveSpeedMultiplierPerStack
        );

        m_DetectionMultiplierPerStack = Mathf.Max(0f, m_DetectionMultiplierPerStack);

        if (string.IsNullOrWhiteSpace(m_StatusId))
            m_StatusId = name;
    } //private void OnValidate()
#endif
} //public class ActionStatusDefinition