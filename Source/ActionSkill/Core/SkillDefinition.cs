using System;
using UnityEngine;

[Flags]
public enum SkillTag
{
    //전투 특성 Tag 없음
    None = 0,

    //근접 공격 Skill
    Melee = 1 << 0,

    //원거리 공격 Skill
    Ranged = 1 << 1,

    //Projectile을 사용하는 Skill
    Projectile = 1 << 2,

    //범위 효과를 사용하는 Skill
    Area = 1 << 3,

    //Caster 주변에 지속되는 Aura Skill
    Aura = 1 << 4,

    //물리 계열 Skill
    Physical = 1 << 5,

    //마법 계열 Skill
    Magical = 1 << 6,

    //독 계열 Skill
    Poison = 1 << 7,

    //이동 기능을 포함하는 Skill
    Movement = 1 << 8,

    //회복 기능을 포함하는 Skill
    Heal = 1 << 9,

    //아군 또는 자신에게 이로운 효과를 부여하는 Skill
    Buff = 1 << 10,

    //적에게 불리한 효과를 부여하는 Skill
    Debuff = 1 << 11,

    //직업 고정 Basic Attack
    BasicAttack = 1 << 12,

    //금속 계열 효과를 사용하는 Skill
    MetalEffect = 1 << 13
} //public enum SkillTag

[CreateAssetMenu(
    menuName = "Last Expedition/Skill/Skill Definition",
    fileName = "New Skill Definition"
)]
public class SkillDefinition : ScriptableObject
{
    /*
    Skill의 Identity, UI, Resource Cost, Activation, Cooldown, 분류와
    ActionDefinition 및 Upgrade 데이터를 정의하는 정적 Skill 데이터
    */

    private const float MinimumChannelActionInterval = 0.05f;

    [Header("Identity")]
    [SerializeField] private string m_SkillId = "NewSkill";
    [SerializeField] private string m_SkillName = "새 스킬";

    [TextArea(3, 8)]
    [SerializeField] private string m_Description = string.Empty;

    [Header("UI")]
    [SerializeField] private Sprite m_Icon;

    [Tooltip(
        "비워두면 아래 코스트 설정을 기준으로 자동 생성합니다.\n" +
        "특수한 표기가 필요할 때만 직접 입력합니다."
    )]
    [SerializeField] private string m_CostText = string.Empty;

    [Header("Resource Cost")]
    [Tooltip("스킬을 활성화하는 순간 1회 소비되는 고유 자원량입니다.")]
    [Min(0f)]
    [SerializeField] private float m_ResourceCost;

    [Tooltip("토글형 스킬이 유지되는 동안 초당 소비되는 고유 자원량입니다.")]
    [Min(0f)]
    [SerializeField] private float m_ResourceCostPerSecond;

    [Header("Activation")]
    [SerializeField] private SkillActivationMode m_ActivationMode = SkillActivationMode.OneShot;

    [Tooltip(
        "토글형 스킬의 효과를 다시 실행하는 간격입니다.\n" +
        "가스파이프처럼 주변 피해를 반복 적용하는 스킬에 사용합니다."
    )]
    [Min(MinimumChannelActionInterval)]
    [SerializeField] private float m_ChannelActionInterval = 0.4f;

    [Tooltip("토글형 스킬이 켜질 때 즉시 Action을 한 번 실행할지 결정합니다.")]
    [SerializeField] private bool m_ExecuteActionImmediately = true;

    [Header("Cooldown")]
    [Min(0f)]
    [SerializeField] private float m_BaseCooldown;

    [SerializeField]
    private SkillCooldownStartTiming m_CooldownStartTiming =
        SkillCooldownStartTiming.OnActivation;

    [Header("Skill Stack")]
    [Tooltip("활성화하면 스킬 사용 시 Stack 1개 소비.")]
    [SerializeField] private bool m_UseSkillStack;

    [Tooltip("보유 가능한 최대 Skill Stack 수.")]
    [SerializeField, Min(1)] private int m_MaxSkillStackCount = 1;

    [Tooltip("소모된 Skill Stack 1개를 회복하는 시간.")]
    [SerializeField, Min(0.01f)] private float m_SkillStackRecoveryTime = 5f;

    [Header("Owner")]
    [Tooltip("Count는 모든 직업이 사용할 수 있는 공용 스킬입니다.")]
    [SerializeField] private CharicType m_OwnerCharicType = CharicType.Count;

    [Header("Classification")]
    [Tooltip(
        "스킬이 어떤 슬롯 역할을 가지는지 결정합니다.\n" +
        "BasicAttack = 좌클릭 기본 공격\n" +
        "Auxiliary = 우클릭 보조 기술\n" +
        "Major = Q / E / Space 주요 기술\n" +
        "Ultimate = R 궁극기"
    )]
    [SerializeField] private ActiveSkillType m_SkillType = ActiveSkillType.Major;

    [Tooltip(
        "스킬의 전투 특성을 정의합니다.\n" +
        "SkillType과 별개로 근접, 투사체, 이동, 버프 등의 " +
        "효과 특성을 지정합니다."
    )]
    [SerializeField] private SkillTag m_SkillTags = SkillTag.None;

    [Header("Action")]
    [SerializeField] private ActionDefinition m_ActionDefinition;

    [Header("Cast")]
    [Tooltip(
        "조준을 확정한 뒤 실제 스킬이 발동하기까지 걸리는 시간입니다.\n" +
        "0이면 즉시 발동합니다."
    )]
    [Min(0f)]
    [SerializeField] private float m_CastTime;

    [Header("Upgrade")]
    [Tooltip(
        "이 스킬을 한 차례 강화했을 때 적용되는 강화 데이터입니다.\n" +
        "Major와 Ultimate에서 사용합니다."
    )]
    [SerializeField] private SkillUpgradeDefinition m_UpgradeDefinition;

    public string SkillId => m_SkillId ?? string.Empty;
    public string SkillName => m_SkillName ?? string.Empty;
    public string Description => m_Description ?? string.Empty;

    public Sprite Icon => m_Icon;

    public float ResourceCost => Mathf.Max(0f, m_ResourceCost);

    public float ResourceCostPerSecond =>
        IsToggleSkill
            ? Mathf.Max(0f, m_ResourceCostPerSecond)
            : 0f;

    public SkillActivationMode ActivationMode => m_ActivationMode;
    public bool IsToggleSkill => m_ActivationMode == SkillActivationMode.Toggle;

    public float ChannelActionInterval =>
        Mathf.Max(MinimumChannelActionInterval, m_ChannelActionInterval);

    public bool ExecuteActionImmediately => m_ExecuteActionImmediately;

    public float BaseCooldown => Mathf.Max(0f, m_BaseCooldown);
    public SkillCooldownStartTiming CooldownStartTiming => m_CooldownStartTiming;

    public bool UseSkillStack => m_UseSkillStack;
    public int MaxSkillStackCount => Mathf.Max(1, m_MaxSkillStackCount);
    public float SkillStackRecoveryTime => Mathf.Max(0.01f, m_SkillStackRecoveryTime);

    public CharicType OwnerCharicType => m_OwnerCharicType;

    public ActiveSkillType SkillType => m_SkillType;
    public SkillTag SkillTags => m_SkillTags;

    public ActionDefinition ActionDefinition => m_ActionDefinition;

    public float CastTime => Mathf.Max(0f, m_CastTime);

    public SkillUpgradeDefinition UpgradeDefinition => m_UpgradeDefinition;

    public string CostText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(m_CostText) == false)
                return m_CostText;

            return CreateCostText();
        }
    }

    //현재 SkillDefinition을 사용하는 Delegate Skill 생성
    public DelegateCharicSkill CreateDelegateSkill(CharicSkillUseHandler a_UseHandler)
    {
        return new DelegateCharicSkill(
            this,
            a_UseHandler
        );
    } //public DelegateCharicSkill CreateDelegateSkill()

    //지정 Character가 현재 Skill을 사용할 수 있는지 확인
    public bool CanBeUsedBy(CharicType a_CharicType)
    {
        if (m_OwnerCharicType == CharicType.Count)
            return true;

        return m_OwnerCharicType == a_CharicType;
    } //public bool CanBeUsedBy()

    //현재 Resource Cost 설정을 기반으로 UI 표시용 Cost Text 생성
    private string CreateCostText()
    {
        bool hasInstantCost = ResourceCost > 0f;
        bool hasContinuousCost = ResourceCostPerSecond > 0f;

        if (hasInstantCost == false && hasContinuousCost == false)
            return string.Empty;

        if (hasInstantCost && hasContinuousCost)
        {
            return
                $"자원 {ResourceCost:0.##} 소모 / " +
                $"초당 {ResourceCostPerSecond:0.##}";
        }

        if (hasInstantCost)
            return $"자원 {ResourceCost:0.##} 소모";

        return $"초당 자원 {ResourceCostPerSecond:0.##} 소모";
    } //private string CreateCostText()

#if UNITY_EDITOR
    //Inspector 변경 시 Skill 설정값 검증 및 유효 범위 보정
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_SkillId))
            m_SkillId = name.Replace(" ", "_");

        if (m_UpgradeDefinition != null &&
            m_SkillType != ActiveSkillType.Major &&
            m_SkillType != ActiveSkillType.Ultimate)
        {
            Debug.LogWarning(
                $"[{name}] SkillUpgradeDefinition은 Major 또는 Ultimate 타입에서만 사용합니다.",
                this
            );
        }

        m_CastTime = Mathf.Max(0f, m_CastTime);
        m_ResourceCost = Mathf.Max(0f, m_ResourceCost);
        m_ResourceCostPerSecond = Mathf.Max(0f, m_ResourceCostPerSecond);
        m_ChannelActionInterval = Mathf.Max(MinimumChannelActionInterval, m_ChannelActionInterval);
        m_BaseCooldown = Mathf.Max(0f, m_BaseCooldown);
        m_MaxSkillStackCount = Mathf.Max(1, m_MaxSkillStackCount);
        m_SkillStackRecoveryTime = Mathf.Max(0.01f, m_SkillStackRecoveryTime);
    } //private void OnValidate()
#endif
} //public class SkillDefinition