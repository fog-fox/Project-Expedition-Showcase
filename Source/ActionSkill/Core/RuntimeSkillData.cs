using System;
using UnityEngine;

public class RuntimeActionEffectData
{
    /*
    ActionEffectInfo의 원본 설정을 기반으로 생성되어
    Skill Upgrade, Equipment, Passive가 수정할 Runtime Effect 데이터를 관리
    */

    public ActionEffectInfo SourceEffect { get; }

    public string EffectId { get; }
    public ActionEffectType EffectType { get; }
    public ActionApplyMode ApplyMode { get; }
    public DamageType DamageType { get; }
    public bool CanCritical { get; set; }
    public float CriticalChance { get; set; }
    public float CriticalDamageMultiplier { get; set; }
    public bool IsCritical { get; private set; }
    public ActionStatusDefinition StatusDefinition { get; }

    public int StatusStackAmount { get; set; }

    public float ValuePerApply { get; set; }
    public float Duration { get; set; }
    public float TickInterval { get; set; }
    public float KnockbackPower { get; set; }

    public float MoveDistance { get; set; }
    public float MoveDuration { get; set; }
    public float StopDistance { get; set; }
    public float PullForce { get; set; }
    public float PullMaxSpeed { get; set; }

    public float StatusDuration { get; set; }
    public float StatusTickInterval { get; set; }
    public float StatusReapplyInterval { get; set; }

    public float StatusValueScale { get; set; } = 1f;
    public float StatusModifierScale { get; set; } = 1f;

    public float ReactionHealMultiplier { get; set; } = 1f;
    public float ReactionBuffMultiplier { get; set; } = 1f;

    public ActionEffectTargetApplicationMode TargetApplicationMode { get; }
    public ActionPushDirectionMode PushDirectionMode { get; }
    public ActionDashDirectionMode DashDirectionMode { get; }

    private float m_CriticalRoll = 1f;
    private bool m_HasCriticalRoll;

    private GameObject m_TargetCriticalChanceBonusTarget;
    private float m_TargetCriticalChanceBonus;

    //ActionEffectInfo를 기반으로 독립적인 Runtime Effect 데이터 생성
    public RuntimeActionEffectData(ActionEffectInfo a_SourceEffect)
    {
        SourceEffect = a_SourceEffect;
        EffectId = a_SourceEffect != null ? a_SourceEffect.EffectId ?? string.Empty : string.Empty;

        if (a_SourceEffect == null) return;

        EffectType = a_SourceEffect.EffectType;
        ApplyMode = a_SourceEffect.ApplyMode;
        DamageType = a_SourceEffect.DamageType;

        CanCritical = a_SourceEffect.CanCritical;
        CriticalChance = a_SourceEffect.CriticalChance;
        CriticalDamageMultiplier = a_SourceEffect.CriticalDamageMultiplier;

        StatusDefinition = a_SourceEffect.StatusDefinition;
        StatusStackAmount = a_SourceEffect.StatusStackAmount;

        ValuePerApply = a_SourceEffect.ValuePerApply;
        Duration = a_SourceEffect.Duration;
        TickInterval = a_SourceEffect.TickInterval;
        KnockbackPower = a_SourceEffect.KnockbackPower;

        MoveDistance = a_SourceEffect.MoveDistance;
        MoveDuration = a_SourceEffect.MoveDuration;
        StopDistance = a_SourceEffect.StopDistance;
        DashDirectionMode = a_SourceEffect.DashDirectionMode;

        PullForce = a_SourceEffect.PullForce;
        PullMaxSpeed = a_SourceEffect.PullMaxSpeed;

        StatusDuration = a_SourceEffect.StatusDuration;
        StatusTickInterval = a_SourceEffect.StatusTickInterval;
        StatusReapplyInterval = a_SourceEffect.StatusReapplyInterval;

        TargetApplicationMode = a_SourceEffect.TargetApplicationMode;
        PushDirectionMode = a_SourceEffect.PushDirectionMode;
    } //public RuntimeActionEffectData()

    //현재 Runtime Damage Effect의 치명타 난수와 기본 치명타 여부를 한 번 확정
    public void ResolveCritical()
    {
        IsCritical = false;
        m_HasCriticalRoll = false;
        m_CriticalRoll = 1f;

        if (EffectType != ActionEffectType.Damage || CanCritical == false)
            return;

        m_CriticalRoll = UnityEngine.Random.value;
        m_HasCriticalRoll = true;
        IsCritical = m_CriticalRoll < Mathf.Clamp01(CriticalChance);
    } //public void ResolveCritical()

    //특정 대상에게만 적용되는 치명타 확률 보너스 추가
    public void AddTargetCriticalChanceBonus(
        GameObject a_TargetObject,
        float a_BonusChance)
    {
        if (a_TargetObject == null || a_BonusChance <= 0f)
            return;

        if (m_TargetCriticalChanceBonusTarget != null &&
            m_TargetCriticalChanceBonusTarget != a_TargetObject)
        {
            return;
        }

        m_TargetCriticalChanceBonusTarget = a_TargetObject;
        m_TargetCriticalChanceBonus = Mathf.Clamp01(
            m_TargetCriticalChanceBonus + a_BonusChance
        );
    } //public void AddTargetCriticalChanceBonus()

    //실제 피격 대상까지 반영한 최종 치명타 여부 반환
    public bool IsCriticalAgainstTarget(GameObject a_TargetObject)
    {
        if (CanCritical == false || m_HasCriticalRoll == false)
            return false;

        float criticalChance = CriticalChance;

        if (a_TargetObject != null &&
            a_TargetObject == m_TargetCriticalChanceBonusTarget)
        {
            criticalChance += m_TargetCriticalChanceBonus;
        }

        return m_CriticalRoll < Mathf.Clamp01(criticalChance);
    } //public bool IsCriticalAgainstTarget()
} //public class RuntimeActionEffectData

public readonly struct RuntimePassiveStackConsumption
{
    /*
    Runtime Skill 생성 시 특정 Passive가 스냅샷한
    소비 예정 Stack 수를 Passive ID와 함께 보관하는 불변 데이터
    */

    public string PassiveId { get; }
    public int StackCount { get; }

    //Passive ID와 해당 공격이 소비할 Stack 수 초기화
    public RuntimePassiveStackConsumption(
        string a_PassiveId,
        int a_StackCount)
    {
        PassiveId = a_PassiveId ?? string.Empty;
        StackCount = Mathf.Max(0, a_StackCount);
    } //public RuntimePassiveStackConsumption()
} //public readonly struct RuntimePassiveStackConsumption

public class RuntimeSkillData
{
    /*
    SkillDefinition과 ActionDefinition의 기본값을 복사하고 현재 Action State Variant를 반영한 뒤
    Skill Upgrade, Equipment, Passive가 적용된 최종 Runtime Skill 데이터를 관리
    */

    private const float MinimumRadius = 0.1f;
    private const float MinimumDuration = 0.01f;
    private const float MinimumTickInterval = 0.05f;

    private const float MinimumStatusRefreshMargin = 0.1f;
    private const float StatusRefreshMarginRatio = 0.25f;
    private const float DefaultHostileTickInterval = 0.2f;

    public SkillDefinition SkillDefinition { get; }
    public ActionDefinition ActionDefinition { get; }

    public ActionDashDirectionMode DashDirectionMode { get; }

    public ActionTargetMode TargetMode { get; set; }

    public float Radius { get; set; }

    public float ProjectileSpeed { get; set; }

    public float ProjectileLifeTime { get; set; }
    public float ProjectileSpawnDistance { get; set; }

    public int ProjectileCount { get; set; }

    public float ProjectileSpreadAngle { get; set; }

    public bool GrowProjectileWhileMoving { get; set; }

    public float ProjectileStartScaleRatio { get; set; }
    public float ProjectileEndScaleRatio { get; set; }

    public AnimationCurve ProjectileGrowthCurve { get; private set; }

    public RuntimeActionEffectData[] Effects { get; private set; } =
        Array.Empty<RuntimeActionEffectData>();

    private RuntimePassiveStackConsumption[] m_PassiveStackConsumptions =
        Array.Empty<RuntimePassiveStackConsumption>();

    public bool ApplyEffectsWhileProjectileActive { get; set; }
    public float ProjectileEffectCheckInterval { get; set; }
    public float ProjectileVisualBaseRadius { get; set; }

    public ActionTargetFilter TargetFilter { get; set; }
    public LayerMask TargetLayerMask { get; set; }

    public ActionAreaShape AreaShape { get; set; }
    public float SectorAngle { get; set; }

    public bool FollowCasterWhileActive { get; set; }

    //Skill과 Action의 기본값을 복사하여 Runtime Skill 데이터 생성
    private RuntimeSkillData(
        SkillDefinition a_SkillDefinition,
        ActionDefinition a_ActionDefinition)
    {
        SkillDefinition = a_SkillDefinition;
        ActionDefinition = a_ActionDefinition;

        TargetMode = a_ActionDefinition.TargetMode;
        Radius = a_ActionDefinition.Radius;

        TargetFilter = a_ActionDefinition.TargetFilter;
        AreaShape = a_ActionDefinition.AreaShape;
        SectorAngle = a_ActionDefinition.SectorAngle;

        FollowCasterWhileActive = a_ActionDefinition.FollowCasterWhileActive;

        ProjectileSpeed = a_ActionDefinition.ProjectileSpeed;
        ProjectileLifeTime = a_ActionDefinition.ProjectileLifeTime;
        ProjectileSpawnDistance = a_ActionDefinition.ProjectileSpawnDistance;
        ProjectileCount = a_ActionDefinition.ProjectileCount;
        ProjectileSpreadAngle = a_ActionDefinition.ProjectileSpreadAngle;

        GrowProjectileWhileMoving = a_ActionDefinition.GrowProjectileWhileMoving;
        ProjectileStartScaleRatio = a_ActionDefinition.ProjectileStartScaleRatio;
        ProjectileEndScaleRatio = a_ActionDefinition.ProjectileEndScaleRatio;
        ProjectileGrowthCurve = a_ActionDefinition.ProjectileGrowthCurve;

        ApplyEffectsWhileProjectileActive = a_ActionDefinition.ApplyEffectsWhileProjectileActive;
        ProjectileEffectCheckInterval = a_ActionDefinition.ProjectileEffectCheckInterval;
        ProjectileVisualBaseRadius = a_ActionDefinition.ProjectileVisualBaseRadius;

        ReplaceEffects(a_ActionDefinition.Effects);
    } //private RuntimeSkillData()

    //SkillDefinition을 기준으로 최종 Runtime Skill 데이터 생성
    public static RuntimeSkillData Create(
        SkillDefinition a_SkillDefinition,
        ActionUserContext a_Context)
    {
        if (a_SkillDefinition == null) return null;

        ActionDefinition actionDefinition = a_SkillDefinition.ActionDefinition;
        if (actionDefinition == null) return null;

        return CreateInternal(a_SkillDefinition, actionDefinition, a_Context);
    } //public static RuntimeSkillData Create()

    //ActionDefinition에서 연결된 SkillDefinition을 찾아 Runtime Skill 데이터 생성
    public static RuntimeSkillData Create(
        ActionDefinition a_ActionDefinition,
        ActionUserContext a_Context)
    {
        if (a_ActionDefinition == null) return null;

        Charic casterCharic = a_Context != null ? a_Context.GetCasterCharic() : null;
        CharicType charicType = casterCharic != null ? casterCharic.m_ChrType : CharicType.Count;

        SkillDefinition skillDefinition = null;
        SkillDefinitionDatabase database = SkillDefinitionDatabase.Instance;

        if (database != null)
            skillDefinition = database.GetSkillDefinition(a_ActionDefinition, charicType);

        return CreateInternal(skillDefinition, a_ActionDefinition, a_Context);
    } //public static RuntimeSkillData Create()

    //기본값에 State Variant, Skill Upgrade, Equipment, Passive 순서로 Runtime Modifier 적용
    private static RuntimeSkillData CreateInternal(
        SkillDefinition a_SkillDefinition,
        ActionDefinition a_ActionDefinition,
        ActionUserContext a_Context)
    {
        RuntimeSkillData data = new RuntimeSkillData(a_SkillDefinition, a_ActionDefinition);

        Charic casterCharic = a_Context != null ? a_Context.GetCasterCharic() : null;
        Player casterPlayer = a_Context != null ? a_Context.GetCasterPlayer() : null;

        /*
        ActionDefinition 기본값
        → Context를 포함한 기본 Target Layer
        → Action State Variant
        → Skill 자체 Upgrade
        → Equipment
        → Passive
        */
        data.TargetLayerMask = a_ActionDefinition.GetTargetLayerMask(a_Context);
        data.ApplyStateVariant(casterCharic);
        data.ApplyInheritedEffectMultipliers(a_Context);

        if (a_SkillDefinition != null &&
            casterPlayer != null &&
            casterPlayer.IsSkillUpgraded(a_SkillDefinition))
        {
            SkillUpgradeDefinition upgrade = a_SkillDefinition.UpgradeDefinition;
            upgrade?.ApplyTo(data);
        }

        if (casterCharic != null && a_SkillDefinition != null)
        {
            casterCharic.ApplyEquipmentSkillModifiers(a_SkillDefinition, data);

            ICharicPassiveProvider passiveProvider = casterCharic as ICharicPassiveProvider;
            passiveProvider?.ApplyPassiveModifiers(a_SkillDefinition, data);
        }

        data.ClampValues();
        data.ResolveCriticalResults();

        return data;
    } //private static RuntimeSkillData CreateInternal()

    //상위 Action에서 전달된 Heal 및 Buff 강화 배율을 현재 Runtime Effect에 적용
    private void ApplyInheritedEffectMultipliers(
        ActionUserContext a_Context)
    {
        if (a_Context == null)
            return;

        float healMultiplier =
            a_Context.GetInheritedHealMultiplier();

        float buffMultiplier =
            a_Context.GetInheritedBuffMultiplier();

        if (Mathf.Approximately(healMultiplier, 1f) == false)
            ApplyHealMultiplier(healMultiplier);

        if (Mathf.Approximately(buffMultiplier, 1f) == false)
            ApplyBuffMultiplier(buffMultiplier);
    } //private void ApplyInheritedEffectMultipliers()

    //현재 Runtime Skill의 모든 회복 Effect에 지정 배율 적용
    private void ApplyHealMultiplier(
        float a_Multiplier)
    {
        float multiplier =
            Mathf.Max(0f, a_Multiplier);

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect =
                Effects[i];

            if (effect == null)
                continue;

            if (effect.EffectType == ActionEffectType.Heal ||
                effect.EffectType == ActionEffectType.HealWithOverhealShield)
            {
                effect.ValuePerApply *= multiplier;
                continue;
            }

            if (effect.EffectType == ActionEffectType.ApplyStatus &&
                effect.StatusDefinition != null &&
                effect.StatusDefinition.EffectType == ActionStatusEffectType.Heal)
            {
                effect.StatusValueScale *= multiplier;
            }
        }
    } //private void ApplyHealMultiplier()

    //현재 Runtime Skill의 모든 Buff Effect에 지정 배율 적용
    private void ApplyBuffMultiplier(
        float a_Multiplier)
    {
        float multiplier =
            Mathf.Max(0f, a_Multiplier);

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect =
                Effects[i];

            if (effect == null)
                continue;

            if (effect.EffectType == ActionEffectType.ApplyShield)
            {
                effect.ValuePerApply *= multiplier;
                continue;
            }

            if (effect.EffectType == ActionEffectType.ApplyStatus &&
                effect.StatusDefinition != null &&
                effect.StatusDefinition.HasTag(ActionStatusTag.Buff))
            {
                effect.StatusModifierScale *= multiplier;
            }
        }
    } //private void ApplyBuffMultiplier()

    //Caster의 현재 Action State와 일치하는 Variant를 Runtime 데이터에 적용
    private void ApplyStateVariant(Charic a_CasterCharic)
    {
        if (a_CasterCharic == null || ActionDefinition == null) return;

        IActionStateProvider stateProvider = a_CasterCharic as IActionStateProvider;
        if (stateProvider == null) return;

        string stateId = stateProvider.ActionStateId;
        if (string.IsNullOrWhiteSpace(stateId)) return;

        if (ActionDefinition.TryGetStateVariant(stateId, out ActionStateVariant variant) == false)
            return;

        if (variant.OverrideTargetMode)
            TargetMode = variant.TargetMode;

        if (variant.OverrideTargetFilter)
            TargetFilter = variant.TargetFilter;

        if (variant.OverrideTargetLayerMask)
            TargetLayerMask = variant.TargetLayerMask;

        if (variant.OverrideRadius)
            Radius = variant.Radius;

        if (variant.OverrideAreaShape)
            AreaShape = variant.AreaShape;

        if (variant.OverrideSectorAngle)
            SectorAngle = variant.SectorAngle;

        ApplyStateVariantEffects(variant);
    } //private void ApplyStateVariant()

    //State Variant의 Effect 처리 방식에 따라 Runtime Effect 유지, 교체 또는 추가
    private void ApplyStateVariantEffects(ActionStateVariant a_Variant)
    {
        if (a_Variant == null) return;

        switch (a_Variant.EffectMode)
        {
            case ActionStateEffectMode.KeepBase:
                return;

            case ActionStateEffectMode.Replace:
                ReplaceEffects(a_Variant.Effects);
                return;

            case ActionStateEffectMode.Append:
                AppendEffects(a_Variant.Effects);
                return;
        }
    } //private void ApplyStateVariantEffects()

    //ActionEffectInfo 배열을 기존 Runtime Effect 뒤에 추가
    private void AppendEffects(ActionEffectInfo[] a_SourceEffects)
    {
        if (a_SourceEffects == null || a_SourceEffects.Length <= 0)
            return;

        int validEffectCount = 0;

        for (int i = 0; i < a_SourceEffects.Length; i++)
        {
            if (a_SourceEffects[i] != null)
                validEffectCount++;
        }

        if (validEffectCount <= 0)
            return;

        int currentCount = Effects.Length;

        RuntimeActionEffectData[] newEffects =
            new RuntimeActionEffectData[currentCount + validEffectCount];

        if (currentCount > 0)
            Array.Copy(Effects, newEffects, currentCount);

        int writeIndex = currentCount;

        for (int i = 0; i < a_SourceEffects.Length; i++)
        {
            ActionEffectInfo sourceEffect = a_SourceEffects[i];

            if (sourceEffect == null)
                continue;

            newEffects[writeIndex] = new RuntimeActionEffectData(sourceEffect);
            writeIndex++;
        }

        Effects = newEffects;
    } //private void AppendEffects()

    private void ReplaceEffects(ActionEffectInfo[] a_SourceEffects)
    {
        if (a_SourceEffects == null || a_SourceEffects.Length <= 0)
        {
            Effects = Array.Empty<RuntimeActionEffectData>();
            return;
        }

        Effects = new RuntimeActionEffectData[a_SourceEffects.Length];

        for (int i = 0; i < a_SourceEffects.Length; i++)
        {
            ActionEffectInfo sourceEffect = a_SourceEffects[i];

            if (sourceEffect == null) continue;

            Effects[i] = new RuntimeActionEffectData(sourceEffect);
        }
    } //private void ReplaceEffects()

    //모든 Damage Effect에 지정 배율 적용
    public void ApplyDamageMultiplier(float a_Multiplier)
    {
        float multiplier = Mathf.Max(0f, a_Multiplier);

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || effect.EffectType != ActionEffectType.Damage)
                continue;

            effect.ValuePerApply = Mathf.Max(0f, effect.ValuePerApply * multiplier);
        }

        ClampValues();
    } //public void ApplyDamageMultiplier()

    //Skill Radius에 지정 배율 적용
    public void ApplyRadiusMultiplier(float a_Multiplier)
    {
        float multiplier = Mathf.Max(0f, a_Multiplier);

        Radius *= multiplier;

        ClampValues();
    } //public void ApplyRadiusMultiplier()

    //Equipment Skill Modifier를 Runtime Skill 데이터에 적용
    public void ApplyModifier(EquipmentSkillModifier a_Modifier)
    {
        if (a_Modifier == null) return;

        switch (a_Modifier.TargetValue)
        {
            case SkillModifierTarget.Radius:
                Radius = ApplyValue(Radius, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillModifierTarget.ProjectileSpeed:
                ProjectileSpeed = ApplyValue(ProjectileSpeed, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillModifierTarget.ProjectileLifeTime:
                ProjectileLifeTime = ApplyValue(ProjectileLifeTime, a_Modifier.Operation, a_Modifier.Value);
                break;
        }

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || IsEffectMatched(a_Modifier, effect) == false)
                continue;

            ApplyEffectModifier(effect, a_Modifier);
        }
    } //public void ApplyModifier()

    //Passive Skill Value Modifier를 Runtime Skill 데이터에 적용
    public void ApplyPassiveModifier(PassiveModifier a_Modifier)
    {
        if (a_Modifier == null ||
            a_Modifier.ModifierType != PassiveModifierType.ModifySkillValue)
        {
            return;
        }

        switch (a_Modifier.SkillValueTarget)
        {
            case PassiveSkillValueTarget.None:
                return;

            case PassiveSkillValueTarget.Radius:
                Radius = ApplyValue(Radius, a_Modifier.Operation, a_Modifier.Value);
                break;

            case PassiveSkillValueTarget.Damage:
                ApplyEffectValueModifier(a_Modifier, ActionEffectType.Damage);
                break;

            case PassiveSkillValueTarget.Heal:
                ApplyHealEffectModifier(a_Modifier);
                break;

            case PassiveSkillValueTarget.BuffEffect:
                ApplyBuffEffectModifier(a_Modifier);
                break;

            case PassiveSkillValueTarget.Duration:
                ApplyEffectDurationModifier(a_Modifier);
                break;

            case PassiveSkillValueTarget.TickInterval:
                ApplyEffectTickModifier(a_Modifier);
                break;

            case PassiveSkillValueTarget.ProjectileSpeed:
                ProjectileSpeed = ApplyValue(ProjectileSpeed, a_Modifier.Operation, a_Modifier.Value);
                break;

            case PassiveSkillValueTarget.ProjectileLifeTime:
                ProjectileLifeTime = ApplyValue(ProjectileLifeTime, a_Modifier.Operation, a_Modifier.Value);
                break;

            case PassiveSkillValueTarget.ProjectileCount:
                float modifiedCount = ApplyValue(
                    ProjectileCount,
                    a_Modifier.Operation,
                    a_Modifier.Value
                );

                ProjectileCount = Mathf.RoundToInt(modifiedCount);
                break;

            case PassiveSkillValueTarget.CriticalChance:
                ApplyCriticalChanceModifier(a_Modifier);
                break;

            case PassiveSkillValueTarget.CriticalDamageMultiplier:
                ApplyCriticalDamageMultiplierModifier(a_Modifier);
                break;

            /*
            ResourceCost와 Cooldown은 현재 RuntimeSkillData가
            직접 소유하지 않으므로 이 경로에서는 처리하지 않음.
            */
            case PassiveSkillValueTarget.ResourceCost:
            case PassiveSkillValueTarget.Cooldown:
                break;
        }
    } //public void ApplyPassiveModifier()

    //Passive가 추가하는 Target Status Effect를 Runtime Effect 뒤에 등록
    public bool AddPassiveTargetStatusEffect(PassiveModifier a_Modifier)
    {
        if (a_Modifier == null ||
            a_Modifier.ModifierType != PassiveModifierType.AddStatusToSkillTarget)
        {
            return false;
        }

        ActionStatusDefinition statusDefinition = a_Modifier.StatusDefinition;

        if (statusDefinition == null || ContainsPassiveStatusEffect(statusDefinition))
            return false;

        float statusDuration = ResolvePassiveTargetStatusDuration(a_Modifier);
        if (statusDuration <= 0f) return false;

        string effectId = "PassiveTargetStatus_" + statusDefinition.StatusId;

        ActionEffectInfo sourceEffect = ActionEffectInfo.CreateRuntimeStatusEffect(
            effectId,
            ActionApplyMode.Instant,
            statusDuration,
            1f,
            statusDefinition,
            a_Modifier.StatusStackAmount,
            statusDuration,
            a_Modifier.StatusReapplyInterval
        );

        if (sourceEffect == null) return false;

        AppendRuntimeEffect(new RuntimeActionEffectData(sourceEffect));

        return true;
    } //public bool AddPassiveTargetStatusEffect()

    //Passive가 추가하는 Status Effect를 적대적 지속 Effect 주기에 맞춰 등록
    public bool AddPassiveStatusEffect(
        PassiveModifier a_Modifier)
    {
        if (a_Modifier == null || a_Modifier.ModifierType != PassiveModifierType.AddStatusEffect)
            return false;

        ActionStatusDefinition statusDefinition = a_Modifier.StatusDefinition;

        if (statusDefinition == null || ContainsPassiveStatusEffect(statusDefinition))
            return false;

        if (TryGetHostileOverTimeTiming(
                out float effectTickInterval,
                out float effectDuration) == false)
        {
            return false;
        }

        float refreshSafetyMargin = Mathf.Max(
            MinimumStatusRefreshMargin,
            effectTickInterval * StatusRefreshMarginRatio
        );

        float minimumStatusDuration = effectTickInterval + refreshSafetyMargin;

        float requestedStatusDuration =
            a_Modifier.StatusDuration > 0f
                ? a_Modifier.StatusDuration
                : minimumStatusDuration;

        float statusDuration = Mathf.Max(
            requestedStatusDuration,
            minimumStatusDuration
        );

        string effectId = "PassiveStatus_" + statusDefinition.StatusId;

        ActionEffectInfo sourceEffect =
            ActionEffectInfo.CreateRuntimeStatusEffect(
                effectId,
                ActionApplyMode.OverTime,
                effectDuration,
                effectTickInterval,
                statusDefinition,
                a_Modifier.StatusStackAmount,
                statusDuration,
                a_Modifier.StatusReapplyInterval
            );

        if (sourceEffect == null)
            return false;

        PrependRuntimeEffect(
            new RuntimeActionEffectData(sourceEffect)
        );

        return true;
    } //public bool AddPassiveStatusEffect()

    //Skill Upgrade Modifier를 Runtime Skill 또는 Effect 값에 적용
    public void ApplyUpgradeModifier(SkillUpgradeModifier a_Modifier)
    {
        if (a_Modifier == null) return;

        switch (a_Modifier.Target)
        {
            case SkillUpgradeValueTarget.None:
                return;

            case SkillUpgradeValueTarget.Radius:
                Radius = ApplyValue(Radius, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillUpgradeValueTarget.SectorAngle:
                SectorAngle = ApplyValue(SectorAngle, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillUpgradeValueTarget.ProjectileSpeed:
                ProjectileSpeed = ApplyValue(ProjectileSpeed, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillUpgradeValueTarget.ProjectileLifeTime:
                ProjectileLifeTime = ApplyValue(ProjectileLifeTime, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillUpgradeValueTarget.ProjectileSpawnDistance:
                ProjectileSpawnDistance = ApplyValue(ProjectileSpawnDistance, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillUpgradeValueTarget.ProjectileCount:
                ProjectileCount = Mathf.RoundToInt(
                    ApplyValue(ProjectileCount, a_Modifier.Operation, a_Modifier.Value)
                );
                break;

            case SkillUpgradeValueTarget.ProjectileSpreadAngle:
                ProjectileSpreadAngle = ApplyValue(
                    ProjectileSpreadAngle,
                    a_Modifier.Operation,
                    a_Modifier.Value
                );
                break;
        }

        ApplyUpgradeEffectModifier(a_Modifier);
    } //public void ApplyUpgradeModifier()

    //Passive가 특정 Effect Type의 ValuePerApply에 Modifier 적용
    private void ApplyEffectValueModifier(
        PassiveModifier a_Modifier,
        ActionEffectType a_EffectType)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || effect.EffectType != a_EffectType)
                continue;

            effect.ValuePerApply = ApplyValue(
                effect.ValuePerApply,
                a_Modifier.Operation,
                a_Modifier.Value
            );
        }
    } //private void ApplyEffectValueModifier()

    //Passive가 지속형 Effect의 Duration에 Modifier 적용
    private void ApplyEffectDurationModifier(PassiveModifier a_Modifier)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || effect.ApplyMode != ActionApplyMode.OverTime) continue;

            effect.Duration = ApplyValue(effect.Duration, a_Modifier.Operation, a_Modifier.Value);
        }
    } //private void ApplyEffectDurationModifier()

    //Passive가 OverTime Effect의 Tick Interval에 Modifier 적용
    private void ApplyEffectTickModifier(PassiveModifier a_Modifier)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || effect.ApplyMode != ActionApplyMode.OverTime)
                continue;

            effect.TickInterval = ApplyValue(
                effect.TickInterval,
                a_Modifier.Operation,
                a_Modifier.Value
            );
        }
    } //private void ApplyEffectTickModifier()

    //Equipment Modifier의 Effect ID 조건 충족 여부 확인
    private static bool IsEffectMatched(
        EquipmentSkillModifier a_Modifier,
        RuntimeActionEffectData a_Effect)
    {
        string targetEffectId = a_Modifier.TargetEffectId;

        if (string.IsNullOrWhiteSpace(targetEffectId))
            return true;

        return string.Equals(targetEffectId, a_Effect.EffectId, StringComparison.Ordinal);
    } //private static bool IsEffectMatched()

    //Equipment Modifier를 단일 Runtime Effect에 적용
    private static void ApplyEffectModifier(RuntimeActionEffectData a_Effect, EquipmentSkillModifier a_Modifier)
    {
        switch (a_Modifier.TargetValue)
        {
            case SkillModifierTarget.Damage:
                if (a_Effect.EffectType != ActionEffectType.Damage) return;

                a_Effect.ValuePerApply = ApplyValue(a_Effect.ValuePerApply, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillModifierTarget.Heal:
                if (a_Effect.EffectType != ActionEffectType.Heal) return;

                a_Effect.ValuePerApply = ApplyValue(a_Effect.ValuePerApply, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillModifierTarget.Duration:
                if (a_Effect.ApplyMode != ActionApplyMode.OverTime) return;

                a_Effect.Duration = ApplyValue(a_Effect.Duration, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillModifierTarget.TickInterval:
                if (a_Effect.ApplyMode != ActionApplyMode.OverTime) return;

                a_Effect.TickInterval = ApplyValue(a_Effect.TickInterval, a_Modifier.Operation, a_Modifier.Value);
                break;

            case SkillModifierTarget.Knockback:
                if (a_Effect.EffectType != ActionEffectType.Damage) return;

                a_Effect.KnockbackPower = ApplyValue(a_Effect.KnockbackPower, a_Modifier.Operation, a_Modifier.Value);
                break;
        }
    } //private static void ApplyEffectModifier()

    //모든 Runtime Damage Effect의 치명타 여부를 최종 Modifier 적용 후 확정
    private void ResolveCriticalResults()
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null)
                continue;

            effect.ResolveCritical();
        }
    } //private void ResolveCriticalResults()

    //Skill Upgrade Modifier를 조건에 맞는 Runtime Effect에 적용
    private void ApplyUpgradeEffectModifier(SkillUpgradeModifier a_Modifier)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || a_Modifier.MatchesEffect(effect) == false) continue;

            switch (a_Modifier.Target)
            {
                case SkillUpgradeValueTarget.Damage:
                    if (effect.EffectType != ActionEffectType.Damage) continue;

                    effect.ValuePerApply = ApplyValue(effect.ValuePerApply, a_Modifier.Operation, a_Modifier.Value);
                    break;

                case SkillUpgradeValueTarget.Heal:
                    if (effect.EffectType != ActionEffectType.Heal) continue;

                    effect.ValuePerApply = ApplyValue(effect.ValuePerApply, a_Modifier.Operation, a_Modifier.Value);
                    break;

                case SkillUpgradeValueTarget.Duration:
                    if (effect.ApplyMode != ActionApplyMode.OverTime) continue;

                    effect.Duration = ApplyValue(effect.Duration, a_Modifier.Operation, a_Modifier.Value);
                    break;

                case SkillUpgradeValueTarget.TickInterval:
                    if (effect.ApplyMode != ActionApplyMode.OverTime) continue;

                    effect.TickInterval = ApplyValue(effect.TickInterval, a_Modifier.Operation, a_Modifier.Value);
                    break;

                case SkillUpgradeValueTarget.Knockback:
                    if (effect.EffectType != ActionEffectType.Damage) continue;

                    effect.KnockbackPower = ApplyValue(effect.KnockbackPower, a_Modifier.Operation, a_Modifier.Value);
                    break;
            }
        }
    } //private void ApplyUpgradeEffectModifier()

    //Runtime Effect 배열 마지막에 Effect 추가
    private void AppendRuntimeEffect(RuntimeActionEffectData a_Effect)
    {
        if (a_Effect == null) return;

        int currentCount = Effects.Length;
        RuntimeActionEffectData[] newEffects = new RuntimeActionEffectData[currentCount + 1];

        if (currentCount > 0)
            Array.Copy(Effects, newEffects, currentCount);

        newEffects[currentCount] = a_Effect;
        Effects = newEffects;
    } //private void AppendRuntimeEffect()

    //Runtime Effect 배열 앞에 Effect 추가
    private void PrependRuntimeEffect(RuntimeActionEffectData a_Effect)
    {
        if (a_Effect == null) return;

        int currentCount = Effects.Length;
        RuntimeActionEffectData[] newEffects = new RuntimeActionEffectData[currentCount + 1];

        if (currentCount > 0)
            Array.Copy(Effects, 0, newEffects, 1, currentCount);

        newEffects[0] = a_Effect;
        Effects = newEffects;
    } //private void PrependRuntimeEffect()

    //동일 StatusDefinition의 ApplyStatus Effect가 이미 존재하는지 확인
    private bool ContainsPassiveStatusEffect(ActionStatusDefinition a_StatusDefinition)
    {
        if (a_StatusDefinition == null) return false;

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || effect.EffectType != ActionEffectType.ApplyStatus)
                continue;

            if (effect.StatusDefinition == a_StatusDefinition)
                return true;
        }

        return false;
    } //private bool ContainsPassiveStatusEffect()

    //Passive Target Status가 사용할 Duration 계산
    private float ResolvePassiveTargetStatusDuration(PassiveModifier a_Modifier)
    {
        if (a_Modifier == null) return 0f;

        switch (a_Modifier.StatusDurationMode)
        {
            case PassiveStatusDurationMode.Fixed:
                return Mathf.Max(MinimumDuration, a_Modifier.StatusDuration);

            case PassiveStatusDurationMode.UseLongestSkillEffectDuration:
                return GetLongestEffectDuration();

            case PassiveStatusDurationMode.UseMatchingStatusDuration:
                return GetLongestStatusDuration();

            case PassiveStatusDurationMode.LinkedToEffectLifecycle:
            default:
                return 0f;
        }
    } //private float ResolvePassiveTargetStatusDuration()

    //적대적인 OverTime Effect의 가장 짧은 Tick과 가장 긴 지속시간 탐색
    private bool TryGetHostileOverTimeTiming(out float a_TickInterval, out float a_Duration)
    {
        a_TickInterval = float.MaxValue;
        a_Duration = 0f;

        bool found = false;

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null || effect.ApplyMode != ActionApplyMode.OverTime) continue;

            bool isHostileEffect = effect.EffectType == ActionEffectType.Damage ||
                                   effect.EffectType == ActionEffectType.ApplyStatus;

            if (isHostileEffect == false) continue;

            a_TickInterval = Mathf.Min(a_TickInterval, effect.TickInterval);
            a_Duration = Mathf.Max(a_Duration, effect.Duration);
            found = true;
        }

        if (found == false) return false;

        if (a_TickInterval == float.MaxValue)
            a_TickInterval = DefaultHostileTickInterval;

        a_TickInterval = Mathf.Max(MinimumTickInterval, a_TickInterval);
        a_Duration = Mathf.Max(MinimumDuration, a_Duration);

        return true;
    } //private bool TryGetHostileOverTimeTiming()

    //등록된 ApplyStatus Effect 중 가장 긴 Status Duration 반환
    private float GetLongestStatusDuration()
    {
        float longestDuration = 0f;

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null ||
                effect.EffectType != ActionEffectType.ApplyStatus ||
                effect.StatusDefinition == null)
            {
                continue;
            }

            longestDuration = Mathf.Max(longestDuration, effect.StatusDuration);
        }

        return longestDuration;
    } //private float GetLongestStatusDuration()

    //등록된 Effect와 Status 중 가장 긴 Duration 반환
    private float GetLongestEffectDuration()
    {
        float longestDuration = 0f;

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null) continue;

            float duration = Mathf.Max(effect.Duration, effect.StatusDuration);
            longestDuration = Mathf.Max(longestDuration, duration);
        }

        return longestDuration;
    } //private float GetLongestEffectDuration()

    //Passive Heal Modifier를 직접 회복, 회복 Status 및 지연 Reaction에 적용
    private void ApplyHealEffectModifier(
        PassiveModifier a_Modifier)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null)
                continue;

            switch (effect.EffectType)
            {
                case ActionEffectType.Heal:
                case ActionEffectType.HealWithOverhealShield:
                    effect.ValuePerApply =
                        ApplyValue(
                            effect.ValuePerApply,
                            a_Modifier.Operation,
                            a_Modifier.Value
                        );
                    break;

                case ActionEffectType.ApplyStatus:
                    if (effect.StatusDefinition == null ||
                        effect.StatusDefinition.EffectType !=
                        ActionStatusEffectType.Heal)
                    {
                        break;
                    }

                    effect.StatusValueScale =
                        ApplyValue(
                            effect.StatusValueScale,
                            a_Modifier.Operation,
                            a_Modifier.Value
                        );
                    break;

                case ActionEffectType.DeployReactiveMine:
                    effect.ReactionHealMultiplier =
                        ApplyValue(
                            effect.ReactionHealMultiplier,
                            a_Modifier.Operation,
                            a_Modifier.Value
                        );
                    break;
            }
        }
    } //private void ApplyHealEffectModifier()

    //치명타가 가능한 모든 Damage Effect의 치명타 확률 변경
    private void ApplyCriticalChanceModifier(PassiveModifier a_Modifier)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null ||
                effect.EffectType != ActionEffectType.Damage ||
                effect.CanCritical == false)
            {
                continue;
            }

            effect.CriticalChance = ApplyValue(
                effect.CriticalChance,
                a_Modifier.Operation,
                a_Modifier.Value
            );
        }
    } //private void ApplyCriticalChanceModifier()

    //치명타가 가능한 모든 Damage Effect의 치명타 피해 배율 변경
    private void ApplyCriticalDamageMultiplierModifier(PassiveModifier a_Modifier)
    {
        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null ||
                effect.EffectType != ActionEffectType.Damage ||
                effect.CanCritical == false)
            {
                continue;
            }

            effect.CriticalDamageMultiplier = ApplyValue(
                effect.CriticalDamageMultiplier,
                a_Modifier.Operation,
                a_Modifier.Value
            );
        }
    } //private void ApplyCriticalDamageMultiplierModifier()

    //Passive Buff Modifier를 보호막, Buff Status 및 지연 Reaction에 적용
    private void ApplyBuffEffectModifier(PassiveModifier a_Modifier)
    {
        float strengthMultiplier = Mathf.Max(0f, ApplyValue(1f, a_Modifier.Operation, a_Modifier.Value));

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null) continue;

            switch (effect.EffectType)
            {
                case ActionEffectType.ApplyShield:
                    effect.ValuePerApply = ApplyValue(effect.ValuePerApply, a_Modifier.Operation, a_Modifier.Value);
                    break;

                case ActionEffectType.ApplyStatus:
                    if (effect.StatusDefinition == null || effect.StatusDefinition.HasTag(ActionStatusTag.Buff) == false)
                        break;

                    effect.StatusModifierScale *= strengthMultiplier;
                    break;

                case ActionEffectType.DeployReactiveMine:
                    effect.ReactionBuffMultiplier *= strengthMultiplier;
                    break;
            }
        }
    } //private void ApplyBuffEffectModifier()

    //현재 Skill의 Critical 가능 Damage Effect에 특정 대상용 치명타 확률 보너스 추가
    public void AddTargetCriticalChanceBonus(
        GameObject a_TargetObject,
        float a_BonusChance)
    {
        if (a_TargetObject == null || a_BonusChance <= 0f)
            return;

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null ||
                effect.EffectType != ActionEffectType.Damage ||
                effect.CanCritical == false)
            {
                continue;
            }

            effect.AddTargetCriticalChanceBonus(
                a_TargetObject,
                a_BonusChance
            );
        }
    } //public void AddTargetCriticalChanceBonus()

    //지정 Passive가 현재 공격 성공 시 소비할 Stack 수 기록
    public void SetPassiveStackConsumption(
        string a_PassiveId,
        int a_StackCount)
    {
        if (string.IsNullOrWhiteSpace(a_PassiveId) ||
            a_StackCount <= 0)
        {
            return;
        }

        for (int i = 0; i < m_PassiveStackConsumptions.Length; i++)
        {
            RuntimePassiveStackConsumption consumption =
                m_PassiveStackConsumptions[i];

            if (string.Equals(
                    consumption.PassiveId,
                    a_PassiveId,
                    StringComparison.Ordinal) == false)
            {
                continue;
            }

            m_PassiveStackConsumptions[i] =
                new RuntimePassiveStackConsumption(
                    a_PassiveId,
                    a_StackCount
                );

            return;
        }

        int currentCount = m_PassiveStackConsumptions.Length;

        RuntimePassiveStackConsumption[] newConsumptions =
            new RuntimePassiveStackConsumption[currentCount + 1];

        if (currentCount > 0)
            Array.Copy(m_PassiveStackConsumptions, newConsumptions, currentCount);

        newConsumptions[currentCount] =
            new RuntimePassiveStackConsumption(
                a_PassiveId,
                a_StackCount
            );

        m_PassiveStackConsumptions = newConsumptions;
    } //public void SetPassiveStackConsumption()

    //지정 Passive가 현재 공격 성공 시 소비할 Stack 수 반환
    public int GetPassiveStackConsumption(string a_PassiveId)
    {
        if (string.IsNullOrWhiteSpace(a_PassiveId))
            return 0;

        for (int i = 0; i < m_PassiveStackConsumptions.Length; i++)
        {
            RuntimePassiveStackConsumption consumption =
                m_PassiveStackConsumptions[i];

            if (string.Equals(
                    consumption.PassiveId,
                    a_PassiveId,
                    StringComparison.Ordinal))
            {
                return Mathf.Max(0, consumption.StackCount);
            }
        }

        return 0;
    } //public int GetPassiveStackConsumption()

    //현재 값에 지정 Modifier 연산 적용
    private static float ApplyValue(
        float a_Current,
        SkillModifierOperation a_Operation,
        float a_Value)
    {
        switch (a_Operation)
        {
            case SkillModifierOperation.AddFlat:
                return a_Current + a_Value;

            case SkillModifierOperation.AddPercent:
                return a_Current * (1f + a_Value);

            case SkillModifierOperation.Multiply:
                return a_Current * a_Value;

            default:
                return a_Current;
        }
    } //private static float ApplyValue()

    //모든 Runtime Skill 값을 유효 범위로 최종 보정
    private void ClampValues()
    {
        Radius = Mathf.Max(MinimumRadius, Radius);
        SectorAngle = Mathf.Clamp(SectorAngle, 1f, 360f);

        ProjectileSpeed = Mathf.Max(0f, ProjectileSpeed);
        ProjectileLifeTime = Mathf.Max(MinimumDuration, ProjectileLifeTime);
        ProjectileSpawnDistance = Mathf.Max(0f, ProjectileSpawnDistance);

        ProjectileCount = Mathf.Max(1, ProjectileCount);
        ProjectileSpreadAngle = Mathf.Clamp(ProjectileSpreadAngle, 0f, 180f);

        ProjectileStartScaleRatio = Mathf.Max(MinimumDuration, ProjectileStartScaleRatio);
        ProjectileEndScaleRatio = Mathf.Max(MinimumDuration, ProjectileEndScaleRatio);

        ProjectileEffectCheckInterval = Mathf.Max(MinimumTickInterval, ProjectileEffectCheckInterval);

        /*
        0은 Projectile Visual Radius를 별도로 지정하지 않고
        Runtime에서 다른 Radius 값을 기준으로 결정하는 상태로 허용.
        */
        ProjectileVisualBaseRadius = Mathf.Max(0f, ProjectileVisualBaseRadius);

        if (ProjectileGrowthCurve == null || ProjectileGrowthCurve.length == 0)
            ProjectileGrowthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        for (int i = 0; i < Effects.Length; i++)
        {
            RuntimeActionEffectData effect = Effects[i];

            if (effect == null) continue;

            effect.ValuePerApply = Mathf.Max(0f, effect.ValuePerApply);
            effect.Duration = Mathf.Max(0f, effect.Duration);
            effect.TickInterval = Mathf.Max(MinimumTickInterval, effect.TickInterval);
            effect.KnockbackPower = Mathf.Max(0f, effect.KnockbackPower);

            effect.CriticalChance = Mathf.Clamp01(effect.CriticalChance);
            effect.CriticalDamageMultiplier = Mathf.Max(1f, effect.CriticalDamageMultiplier);

            effect.MoveDistance = Mathf.Max(0f, effect.MoveDistance);
            effect.MoveDuration = Mathf.Max(MinimumDuration, effect.MoveDuration);
            effect.StopDistance = Mathf.Max(MinimumTickInterval, effect.StopDistance);

            effect.PullForce = Mathf.Max(0f, effect.PullForce);
            effect.PullMaxSpeed = Mathf.Max(MinimumRadius, effect.PullMaxSpeed);

            effect.StatusStackAmount = Mathf.Max(1, effect.StatusStackAmount);
            effect.StatusDuration = Mathf.Max(MinimumDuration, effect.StatusDuration);
            effect.StatusTickInterval = Mathf.Max(MinimumTickInterval, effect.StatusTickInterval);
            effect.StatusReapplyInterval = Mathf.Max(0f, effect.StatusReapplyInterval);

            effect.StatusValueScale = Mathf.Max(0f, effect.StatusValueScale);
            effect.StatusModifierScale = Mathf.Max(0f, effect.StatusModifierScale);
            effect.ReactionHealMultiplier = Mathf.Max(0f, effect.ReactionHealMultiplier);
            effect.ReactionBuffMultiplier = Mathf.Max(0f, effect.ReactionBuffMultiplier);
        }
    } //private void ClampValues()
} //public class RuntimeSkillData