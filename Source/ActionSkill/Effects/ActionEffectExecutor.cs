using UnityEngine;

public static class ActionEffectExecutor
{
    /*
    Action Effect를 유형별 Executor로 전달하고
    실제 피해, 상태, 방어 및 특수 효과 적용을 연결하는 중앙 실행 클래스
    */

    //Runtime Effect 데이터를 기준으로 실제 Effect 적용
    public static void Apply(
        RuntimeActionEffectData a_RuntimeEffect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition)
    {
        if (a_RuntimeEffect == null)
            return;

        ActionEffectInfo sourceEffect = a_RuntimeEffect.SourceEffect;

        if (sourceEffect == null)
            return;

        Apply(
            sourceEffect,
            a_Context,
            a_Target,
            a_ImpactPosition,
            a_RuntimeEffect
        );
    } //public static void Apply()

    //Effect 유형에 따라 담당 Executor로 실제 적용 처리 전달
    public static void Apply(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Effect == null && a_RuntimeEffect == null)
            return;

        ActionEffectType effectType =
            a_RuntimeEffect != null ? a_RuntimeEffect.EffectType : a_Effect.EffectType;

        if (ApplyValueEffect(
                effectType,
                a_Effect,
                a_Context,
                a_Target,
                a_ImpactPosition,
                a_RuntimeEffect))
        {
            return;
        }

        if (ApplyMovementEffect(
                effectType,
                a_Effect,
                a_Context,
                a_Target,
                a_ImpactPosition,
                a_RuntimeEffect))
        {
            return;
        }

        if (ApplyStatusOrDefenseEffect(
                effectType,
                a_Effect,
                a_Context,
                a_Target,
                a_RuntimeEffect))
        {
            return;
        }

        ApplySpecialEffect(
            effectType,
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void Apply()

    //Runtime Effect 데이터를 기준으로 대상에게 적용 가능한지 검증
    public static bool CanApply(
        RuntimeActionEffectData a_RuntimeEffect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        a_FailMessage = null;

        if (a_RuntimeEffect == null)
        {
            a_FailMessage = "실행 효과 데이터가 없습니다.";
            return false;
        }

        ActionEffectInfo sourceEffect = a_RuntimeEffect.SourceEffect;

        if (sourceEffect == null)
        {
            a_FailMessage = "원본 효과 설정이 없습니다.";
            return false;
        }

        return CanApplyInternal(
            sourceEffect,
            a_RuntimeEffect,
            a_Context,
            a_Target,
            out a_FailMessage
        );
    } //public static bool CanApply()

    //원본 Effect 설정을 기준으로 대상에게 적용 가능한지 검증
    public static bool CanApply(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        return CanApplyInternal(
            a_Effect,
            null,
            a_Context,
            a_Target,
            out a_FailMessage
        );
    } //public static bool CanApply()

    //공통 정보와 Effect 유형별 조건을 검사하여 적용 가능 여부 결정
    private static bool CanApplyInternal(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        a_FailMessage = "";

        if (a_Effect == null)
        {
            a_FailMessage = "효과 정보가 없습니다.";
            return false;
        }

        if (a_Context == null)
        {
            a_FailMessage = "액션 실행 정보가 없습니다.";
            return false;
        }

        if (a_Target == null || a_Target.HasTarget() == false)
        {
            a_FailMessage = "효과를 적용할 대상이 없습니다.";
            return false;
        }

        ActionEffectType effectType =
            a_RuntimeEffect != null ? a_RuntimeEffect.EffectType : a_Effect.EffectType;

        switch (effectType)
        {
            case ActionEffectType.ApplyShield:
                return CanApplyShield(
                    a_Effect,
                    a_RuntimeEffect,
                    a_Target,
                    out a_FailMessage
                );

            case ActionEffectType.ArmNextBasicAttack:
                return CanArmNextBasicAttack(
                    a_Context,
                    out a_FailMessage
                );

            case ActionEffectType.DeployWall:
                return CanDeployWall(
                    a_Effect,
                    a_Context,
                    out a_FailMessage
                );

            case ActionEffectType.RegisterTrigger:
                return CanRegisterTrigger(
                    a_Effect,
                    a_Context,
                    a_Target,
                    out a_FailMessage
                );

            case ActionEffectType.Heal:
                return CanApplyHeal(
                    a_Target,
                    a_Effect.RequireMissingRecoverableHealth,
                    a_Effect.RequireMissingRecoverableHealth,
                    out a_FailMessage
                );

            case ActionEffectType.HealWithOverhealShield:
                return CanApplyHeal(
                    a_Target,
                    true,
                    false,
                    out a_FailMessage
                );

            case ActionEffectType.DeployReactiveMine:
                return CanDeployReactiveMine(
                    a_Effect,
                    a_Context,
                    out a_FailMessage
                );

            case ActionEffectType.RemoveInjury:
                return CanRemoveInjury(
                    a_Target,
                    out a_FailMessage
                );
        }

        return true;
    } //private static bool CanApplyInternal()

    //대상이 회복 가능하고 필요 시 실제 회복 가능한 HP가 남아 있는지 확인
    private static bool CanApplyHeal(
        ActionEffectTarget a_Target,
        bool a_RequireHealthValueProvider,
        bool a_RequireMissingRecoverableHealth,
        out string a_FailMessage)
    {
        a_FailMessage = string.Empty;

        if (a_Target == null || a_Target.HasTarget() == false)
        {
            a_FailMessage = "회복을 적용할 대상이 없습니다.";
            return false;
        }

        if (a_Target.GetHealable() == null)
        {
            a_FailMessage = "대상은 회복할 수 없습니다.";
            return false;
        }

        IHealthValueProvider healthProvider = a_Target.GetHealthValueProvider();

        if (a_RequireHealthValueProvider && healthProvider == null)
        {
            a_FailMessage = "대상의 체력 정보를 확인할 수 없습니다.";
            return false;
        }

        if (a_RequireMissingRecoverableHealth == false)
            return true;

        if (healthProvider == null)
        {
            a_FailMessage = "대상의 체력 정보를 확인할 수 없습니다.";
            return false;
        }

        int recoverableMaxHealth = healthProvider.MaxHealth;

        IInjuryValueProvider injuryProvider = a_Target.GetInjuryReceiver();

        if (injuryProvider != null)
            recoverableMaxHealth = Mathf.Min(recoverableMaxHealth, injuryProvider.RecoverableMaxHealth);

        if (healthProvider.CurrentHealth >= recoverableMaxHealth)
        {
            a_FailMessage = "현재 회복 가능한 체력이 최대입니다.";
            return false;
        }

        return true;
    }

    //대상에게 지정된 회복량 적용
    private static void ApplyHeal(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null || a_Target == null)
            return;

        IHealable healable = a_Target.GetHealable();

        if (healable == null)
            return;

        float healAmount = GetValuePerApply(
            a_Effect,
            a_RuntimeEffect
        );

        if (healAmount <= 0f)
            return;

        healable.Heal(
            healAmount,
            a_Context.GetCasterObject(),
            a_Context.m_SourceName
        );
    } //private static void ApplyHeal()

    //대상에게 DamageInfo를 생성하여 피해 적용
    private static void ApplyDamage(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Target == null)
            return;

        IDamageable damageable = a_Target.GetDamageable();

        if (damageable == null)
            return;

        float damageAmount = GetValuePerApply(
            a_Effect,
            a_RuntimeEffect
        );

        if (damageAmount <= 0f)
            return;

        DamageType damageType = GetDamageType(
            a_Effect,
            a_RuntimeEffect
        );

        float knockbackPower = GetKnockbackPower(
            a_Effect,
            a_RuntimeEffect
        );

        GameObject targetObject = a_Target.GetGameObject();

        bool isCritical =
            a_RuntimeEffect != null &&
            a_RuntimeEffect.IsCriticalAgainstTarget(targetObject);

        float criticalDamageMultiplier =
            a_RuntimeEffect != null
                ? a_RuntimeEffect.CriticalDamageMultiplier
                : 1f;

        DamageInfo damageInfo = ActionDamageInfoFactory.Create(
            a_Context,
            a_Target,
            a_ImpactPosition,
            damageAmount,
            damageType,
            knockbackPower,
            isCritical,
            criticalDamageMultiplier
        );

        damageable.TakeDamage(damageInfo);
    } //private static void ApplyDamage()

    //대상을 회복하고 최대 HP를 초과한 회복량을 Shield로 전환
    private static void ApplyHealWithOverhealShield(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null || a_Target == null)
            return;

        IHealable healable = a_Target.GetHealable();
        IHealthValueProvider healthProvider = a_Target.GetHealthValueProvider();

        if (healable == null || healthProvider == null)
            return;

        float healAmount = GetValuePerApply(
            a_Effect,
            a_RuntimeEffect
        );

        int requestedHeal = Mathf.CeilToInt(
            Mathf.Max(0f, healAmount)
        );

        if (requestedHeal <= 0)
            return;

        int missingHealth = Mathf.Max(
            0,
            healthProvider.MaxHealth - healthProvider.CurrentHealth
        );

        int appliedHeal = Mathf.Min(
            requestedHeal,
            missingHealth
        );

        int overhealAmount = Mathf.Max(
            0,
            requestedHeal - appliedHeal
        );

        if (appliedHeal > 0)
        {
            healable.Heal(
                appliedHeal,
                a_Context.GetCasterObject(),
                a_Context.m_SourceName
            );
        }

        if (overhealAmount <= 0)
            return;

        float shieldDuration = GetDuration(
            a_Effect,
            a_RuntimeEffect
        );

        ApplyShieldAmount(
            a_Effect,
            a_Context,
            a_Target,
            overhealAmount,
            shieldDuration
        );
    } //private static void ApplyHealWithOverhealShield()

    //대상에게 설정된 수치의 Shield 적용
    private static void ApplyShield(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null || a_Target == null || a_Target.HasTarget() == false)
            return;

        float shieldAmount = GetValuePerApply(
            a_Effect,
            a_RuntimeEffect
        );

        float shieldDuration = GetDuration(
            a_Effect,
            a_RuntimeEffect
        );

        ApplyShieldAmount(
            a_Effect,
            a_Context,
            a_Target,
            shieldAmount,
            shieldDuration
        );
    } //private static void ApplyShield()

    //지정된 수치와 지속시간으로 대상에게 Shield 적용
    private static void ApplyShieldAmount(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        float a_ShieldAmount,
        float a_Duration)
    {
        if (a_Effect == null ||
            a_Context == null ||
            a_Target == null ||
            a_Target.HasTarget() == false)
        {
            return;
        }

        float shieldAmount = Mathf.Max(
            0f,
            a_ShieldAmount
        );

        float shieldDuration = Mathf.Max(
            0f,
            a_Duration
        );

        if (shieldAmount <= 0f || shieldDuration <= 0f)
            return;

        StackResourceReservationHandle resourceHandle =
            CreateStackResourceHandle(a_Context);

        ShieldController controller =
            ShieldController.GetOrCreate(
                a_Target.GetGameObject()
            );

        if (controller == null)
        {
            resourceHandle?.ReleaseImmediately();
            return;
        }

        string sourceId = GetShieldSourceId(a_Effect);

        string displayName =
            string.IsNullOrWhiteSpace(a_Context.m_SourceName)
                ? sourceId
                : a_Context.m_SourceName;

        ShieldApplication application = new ShieldApplication(
            sourceId,
            a_Context.GetCasterObject(),
            displayName,
            shieldAmount,
            shieldDuration,
            a_Effect.ShieldStackPolicy,
            resourceHandle,
            a_Effect.UseLifecycleEvent,
            a_Effect.LifecycleType,
            a_Effect.LifecycleTags
        );

        bool applied = controller.AddShield(application);

        if (applied == false)
            resourceHandle?.ReleaseImmediately();
    } //private static void ApplyShieldAmount()

    //설정된 방향으로 시전자 Dash 이동 실행
    private static void ApplyDashCaster(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null || a_Context.GetCasterObject() == null)
            return;

        float moveDistance = GetMoveDistance(a_Effect, a_RuntimeEffect);
        float moveDuration = GetMoveDuration(a_Effect, a_RuntimeEffect);

        if (moveDistance <= 0f || moveDuration <= 0f)
            return;

        ActionDashDirectionMode directionMode =
            a_RuntimeEffect != null
                ? a_RuntimeEffect.DashDirectionMode
                : a_Effect.DashDirectionMode;

        Vector2 moveDirection = a_Context.GetUseDirection();

        if (directionMode == ActionDashDirectionMode.ReverseUseDirection)
            moveDirection = -moveDirection;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        ActionMovementRunner runner =
            ActionMovementRunner.GetOrCreate(
                a_Context.GetCasterObject()
            );

        if (runner == null)
            return;

        runner.StartDash(
            a_Context,
            moveDirection,
            moveDistance,
            moveDuration
        );
    } //private static void ApplyDashCaster()

    //대상을 시전자 방향으로 당기기 위한 이동 설정 확인
    private static void ApplyPullTarget(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null ||
            a_Context.GetCasterTransform() == null ||
            a_Target == null ||
            a_Target.HasTarget() == false)
        {
            return;
        }

        float pullForce = GetPullForce(a_Effect, a_RuntimeEffect);
        float pullMaxSpeed = GetPullMaxSpeed(a_Effect, a_RuntimeEffect);

        //이동 제어 API 연결 후 실제 Pull 처리 추가
    } //private static void ApplyPullTarget()

    //설정된 Push 방향을 기준으로 대상을 밀어내기
    private static void ApplyPushTarget(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null ||
            a_Target == null ||
            a_Target.HasTarget() == false)
        {
            return;
        }

        GameObject targetObject = a_Target.GetGameObject();
        Transform targetTransform = a_Target.GetTransform();

        if (targetObject == null || targetTransform == null)
            return;

        Rigidbody2D targetRigidbody = targetObject.GetComponent<Rigidbody2D>();

        if (targetRigidbody == null)
            targetRigidbody = targetObject.GetComponentInChildren<Rigidbody2D>();

        if (targetRigidbody == null)
            return;

        float moveDistance = GetMoveDistance(a_Effect, a_RuntimeEffect);
        float moveDuration = GetMoveDuration(a_Effect, a_RuntimeEffect);

        if (moveDistance <= 0f || moveDuration <= 0f)
            return;

        ActionMovementRunner runner = ActionMovementRunner.GetOrCreate(targetObject);

        if (runner == null)
            return;

        ActionPushDirectionMode pushDirectionMode =
            a_RuntimeEffect != null
                ? a_RuntimeEffect.PushDirectionMode
                : a_Effect.PushDirectionMode;

        Vector2 pushDirection = ResolvePushDirection(
            pushDirectionMode,
            a_Context,
            targetTransform,
            a_ImpactPosition
        );

        runner.StartPushTargetDirection(
            pushDirection,
            targetTransform,
            targetRigidbody,
            moveDistance,
            moveDuration
        );
    } //private static void ApplyPushTarget()

    //Push 설정에 따라 실제 대상 이동 방향 계산
    private static Vector2 ResolvePushDirection(
        ActionPushDirectionMode a_DirectionMode,
        ActionUserContext a_Context,
        Transform a_TargetTransform,
        Vector2 a_ImpactPosition)
    {
        if (a_TargetTransform == null)
            return Vector2.right;

        Vector2 targetPosition = a_TargetTransform.position;

        switch (a_DirectionMode)
        {
            case ActionPushDirectionMode.CasterToTarget:
                if (a_Context != null && a_Context.GetCasterTransform() != null)
                    return targetPosition - a_Context.GetCasterPosition();

                break;

            case ActionPushDirectionMode.UseDirection:
                if (a_Context != null)
                    return a_Context.GetUseDirection();

                break;

            case ActionPushDirectionMode.ImpactCenter:
            default:
                return targetPosition - a_ImpactPosition;
        }

        return targetPosition - a_ImpactPosition;
    } //private static Vector2 ResolvePushDirection()

    //시전자를 대상의 후방으로 이동시키기 위한 설정 확인
    private static void ApplyMoveBehindTarget(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Context == null ||
            a_Context.GetCasterObject() == null ||
            a_Target == null ||
            a_Target.HasTarget() == false)
        {
            return;
        }

        float moveDistance = GetMoveDistance(a_Effect, a_RuntimeEffect);
        float moveDuration = GetMoveDuration(a_Effect, a_RuntimeEffect);
        float stopDistance = GetStopDistance(a_Effect, a_RuntimeEffect);

        Transform targetTransform = a_Target.GetTransform();

        if (targetTransform == null)
            return;

        //이동 제어 API 연결 후 실제 후방 이동 처리 추가
    } //private static void ApplyMoveBehindTarget()

    //대상에게 부착형 Status Effect 적용
    private static void ApplyAttachedStatus(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ActionStatusEffectApplier.Apply(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //private static void ApplyAttachedStatus()

    //대상에게 제거할 부상이 존재하는지 확인
    private static bool CanRemoveInjury(
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        a_FailMessage =
            string.Empty;

        if (a_Target == null ||
            a_Target.HasTarget() == false)
        {
            a_FailMessage =
                "부상을 치료할 대상이 없습니다.";

            return false;
        }

        IInjuryValueProvider injuryReceiver =
            a_Target.GetInjuryReceiver();

        if (injuryReceiver == null)
        {
            a_FailMessage =
                "대상은 부상을 치료할 수 없습니다.";

            return false;
        }

        if (injuryReceiver.CurrentInjuryStack <= 0)
        {
            a_FailMessage =
                "치료할 부상이 없습니다.";

            return false;
        }

        return true;
    } //private static bool CanRemoveInjury()

    //시전자의 다음 기본공격 강화 상태 등록
    private static void ApplyNextBasicAttackEnhancement(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context)
    {
        if (a_Context == null || a_Context.GetCasterObject() == null)
            return;

        StackResourceReservationHandle resourceHandle =
            CreateStackResourceHandle(a_Context);

        NextBasicAttackEnhancementController controller =
            NextBasicAttackEnhancementController.GetOrCreate(a_Context.GetCasterObject());

        if (controller == null)
        {
            resourceHandle?.ReleaseImmediately();
            return;
        }

        string sourceId = string.IsNullOrWhiteSpace(a_Effect.EffectId)
            ? "Metal_Enhancement"
            : a_Effect.EffectId;

        bool armed = controller.Arm(
            sourceId,
            a_Effect.NextBasicAttackDamageMultiplier,
            a_Effect.NextBasicAttackRadiusMultiplier,
            resourceHandle,
            null,
            a_Context.GetCasterObject(),
            a_Context.GetCasterObject(),
            a_Effect.UseLifecycleEvent,
            a_Effect.LifecycleType,
            a_Effect.LifecycleTags
        );

        if (armed == false)
            resourceHandle?.ReleaseImmediately();
    } //private static void ApplyNextBasicAttackEnhancement()

    //대상에게 Trigger Definition을 기반으로 Trigger 등록
    private static void ApplyTrigger(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target)
    {
        if (a_Context == null ||
            a_Context.GetCasterObject() == null ||
            a_Target == null ||
            a_Target.HasTarget() == false ||
            a_Effect.TriggerDefinition == null)
        {
            return;
        }

        GameObject targetObject = a_Target.GetGameObject();

        ActionTriggerController controller =
            ActionTriggerController.GetOrCreate(targetObject);

        if (controller == null)
            return;

        StackResourceReservationHandle resourceHandle =
            CreateStackResourceHandle(a_Context);

        ActionTriggerApplication application = new ActionTriggerApplication(
            a_Effect.TriggerDefinition,
            a_Context.GetCasterObject(),
            a_Context.m_SourceName,
            a_Context.GetAssetProvider(),
            resourceHandle
        );

        bool added = controller.AddTrigger(application);

        if (added == false)
            resourceHandle?.ReleaseImmediately();
    } //private static void ApplyTrigger()

    //시전자 전방에 Metal Wall을 생성하고 Resource 예약 연결
    private static void ApplyDeployWall(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context)
    {
        if (a_Context == null ||
            a_Context.GetCasterObject() == null ||
            a_Context.GetCasterTransform() == null)
        {
            return;
        }

        if (a_Effect.WallPrefab == null)
            return;

        DeployedWallController controller =
            DeployedWallController.GetOrCreate(a_Context.GetCasterObject());

        if (controller == null || controller.HasActiveWall)
            return;

        Vector2 forward = a_Context.m_UseDirection;

        if (forward.sqrMagnitude <= 0.001f)
            forward = a_Context.GetCasterTransform().right;

        forward.Normalize();

        Vector2 spawnPosition =
            (Vector2)a_Context.m_CasterTransform.position +
            forward * a_Effect.WallSpawnDistance;

        float forwardAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        Quaternion wallRotation = Quaternion.Euler(0f, 0f, forwardAngle + 90f);

        StackResourceReservationHandle resourceHandle =
            CreateStackResourceHandle(a_Context);

        MetalWall wall = Object.Instantiate(
            a_Effect.WallPrefab,
            spawnPosition,
            wallRotation
        );

        if (wall == null)
        {
            resourceHandle?.ReleaseImmediately();
            return;
        }

        wall.Initialize(
            a_Effect.WallMaxHealth,
            a_Effect.WallDefense
        );

        bool registered = controller.RegisterWall(
            wall,
            resourceHandle
        );

        if (registered)
            return;

        resourceHandle?.ReleaseImmediately();

        Object.Destroy(wall.gameObject);
    } //private static void ApplyDeployWall()

    //시전자의 Pending Stack Reservation을 Effect 수명 관리용 Handle로 변환
    private static StackResourceReservationHandle CreateStackResourceHandle(
        ActionUserContext a_Context)
    {
        if (a_Context == null)
            return null;

        Charic casterCharic = a_Context.GetCasterCharic();

        if (casterCharic == null)
            return null;

        IStackResourceUser resourceUser = casterCharic as IStackResourceUser;
        IStackResourceReservationSource reservationSource = casterCharic as IStackResourceReservationSource;

        if (resourceUser == null || reservationSource == null)
            return null;

        if (reservationSource.TryTakePendingStackReservation(
                out int reservationId,
                out int stackCount) == false)
        {
            return null;
        }

        if (reservationId <= 0 || stackCount <= 0)
            return null;

        return new StackResourceReservationHandle(
            resourceUser,
            reservationId
        );
    } //private static StackResourceReservationHandle CreateStackResourceHandle()

    //보호막 수치와 중복 적용 조건 확인
    private static bool CanApplyShield(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect,
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        a_FailMessage = "";

        if (a_Target == null || a_Target.HasTarget() == false)
        {
            a_FailMessage = "보호막을 적용할 대상이 없습니다.";
            return false;
        }

        if (GetValuePerApply(a_Effect, a_RuntimeEffect) <= 0f)
        {
            a_FailMessage = "보호막 수치가 올바르지 않습니다.";
            return false;
        }

        if (GetDuration(a_Effect, a_RuntimeEffect) <= 0f)
        {
            a_FailMessage = "보호막 지속시간이 올바르지 않습니다.";
            return false;
        }

        if (a_Effect.BlockIfSameShieldActive == false)
            return true;

        string sourceId = GetShieldSourceId(a_Effect);
        GameObject targetObject = a_Target.GetGameObject();

        ShieldController controller = ShieldController.Find(targetObject);

        if (controller == null || controller.HasShield(sourceId) == false)
            return true;

        a_FailMessage = "대상은 이미 보호 효과를 받고 있습니다.";

        return false;
    } //private static bool CanApplyShield()

    //시전자에게 이미 다음 기본공격 강화가 등록되어 있는지 확인
    private static bool CanArmNextBasicAttack(
        ActionUserContext a_Context,
        out string a_FailMessage)
    {
        a_FailMessage = "";

        if (a_Context == null || a_Context.GetCasterObject() == null)
        {
            a_FailMessage = "강화를 적용할 시전자 정보가 없습니다.";
            return false;
        }

        NextBasicAttackEnhancementController controller =
            NextBasicAttackEnhancementController.Find(a_Context.GetCasterObject());

        if (controller == null || controller.IsArmed == false)
            return true;

        a_FailMessage = "이미 다음 기본공격이 강화되어 있습니다.";

        return false;
    } //private static bool CanArmNextBasicAttack()

    //벽 생성에 필요한 시전자 정보와 기존 활성 벽 존재 여부 확인
    private static bool CanDeployWall(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        out string a_FailMessage)
    {
        a_FailMessage = "";

        if (a_Context == null ||
            a_Context.GetCasterObject() == null ||
            a_Context.GetCasterTransform() == null)
        {
            a_FailMessage = "벽을 생성할 시전자 정보가 없습니다.";
            return false;
        }

        if (a_Effect.WallPrefab == null)
        {
            a_FailMessage = "벽 프리팹이 연결되지 않았습니다.";
            return false;
        }

        DeployedWallController controller =
            DeployedWallController.Find(a_Context.GetCasterObject());

        if (controller != null && controller.HasActiveWall)
        {
            a_FailMessage = "이미 생성된 벽이 있습니다.";
            return false;
        }

        return true;
    } //private static bool CanDeployWall()

    //현재 조준 위치에 반응형 지뢰 설치
    private static void ApplyReactiveMine(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_Effect == null ||
            a_Context == null ||
            a_Context.GetCasterObject() == null ||
            a_Effect.ReactiveMinePrefab == null ||
            a_Effect.ReactiveMineReactionAction == null)
        {
            return;
        }

        GameObject casterObject =
            a_Context.GetCasterObject();

        ReactiveMineController controller =
            ReactiveMineController.GetOrCreate(
                casterObject
            );

        if (controller == null)
            return;

        Vector2 spawnPosition =
            a_Context.m_MouseWorldPosition;

        ReactiveMine mine =
            Object.Instantiate(
                a_Effect.ReactiveMinePrefab,
                spawnPosition,
                Quaternion.identity
            );

        if (mine == null)
            return;

        float reactionHealMultiplier =
            a_RuntimeEffect != null
                ? a_RuntimeEffect.ReactionHealMultiplier
                : 1f;

        float reactionBuffMultiplier =
            a_RuntimeEffect != null
                ? a_RuntimeEffect.ReactionBuffMultiplier
                : 1f;

        if (mine.Initialize(
                a_Effect.ReactiveMineReactionAction,
                a_Context,
                reactionHealMultiplier,
                reactionBuffMultiplier) == false)
        {
            Object.Destroy(mine.gameObject);
            return;
        }

        bool registered =
            controller.RegisterMine(
                mine,
                a_Effect.ReactiveMineMaximumActiveCount
            );

        if (registered)
            return;

        Object.Destroy(mine.gameObject);
    } //private static void ApplyReactiveMine()

    //반응형 지뢰 설치에 필요한 기본 설정 확인
    private static bool CanDeployReactiveMine(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        out string a_FailMessage)
    {
        a_FailMessage = "";

        if (a_Effect == null)
        {
            a_FailMessage = "반응형 지뢰 설정이 없습니다.";
            return false;
        }

        if (a_Context == null ||
            a_Context.GetCasterObject() == null)
        {
            a_FailMessage = "반응형 지뢰를 설치할 시전자 정보가 없습니다.";
            return false;
        }

        if (a_Effect.ReactiveMinePrefab == null)
        {
            a_FailMessage = "반응형 지뢰 Prefab이 연결되지 않았습니다.";
            return false;
        }

        if (a_Effect.ReactiveMineReactionAction == null)
        {
            a_FailMessage = "반응형 지뢰 Reaction Action이 연결되지 않았습니다.";
            return false;
        }

        return true;
    } //private static bool CanDeployReactiveMine()

    //대상에게 동일한 Trigger가 이미 등록되어 있는지 확인
    private static bool CanRegisterTrigger(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        out string a_FailMessage)
    {
        a_FailMessage = "";

        if (a_Context == null || a_Context.GetCasterObject() == null)
        {
            a_FailMessage = "트리거를 적용할 시전자 정보가 없습니다.";
            return false;
        }

        if (a_Target == null || a_Target.HasTarget() == false)
        {
            a_FailMessage = "트리거를 적용할 대상이 없습니다.";
            return false;
        }

        if (a_Effect.TriggerDefinition == null)
        {
            a_FailMessage = "트리거 정의가 연결되지 않았습니다.";
            return false;
        }

        GameObject targetObject = a_Target.GetGameObject();

        ActionTriggerController controller =
            targetObject.GetComponent<ActionTriggerController>();

        if (controller == null)
            return true;

        if (controller.HasTrigger(
                a_Effect.TriggerDefinition.TriggerId,
                a_Context.GetCasterObject()) == false)
        {
            return true;
        }

        a_FailMessage = "대상에게 이미 같은 트리거가 적용되어 있습니다.";

        return false;
    } //private static bool CanRegisterTrigger()

    //보호막을 구분하기 위한 Source ID 반환
    private static string GetShieldSourceId(ActionEffectInfo a_Effect)
    {
        if (a_Effect == null)
            return "ActionShield";

        return string.IsNullOrWhiteSpace(a_Effect.EffectId)
            ? "ActionShield"
            : a_Effect.EffectId;
    } //private static string GetShieldSourceId()

    //Runtime Effect가 있으면 Runtime 적용 수치를 우선 반환
    private static float GetValuePerApply(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.ValuePerApply;

        return a_Effect != null ? a_Effect.ValuePerApply : 0f;
    } //private static float GetValuePerApply()

    //Runtime Effect가 있으면 Runtime 지속시간을 우선 반환
    private static float GetDuration(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.Duration;

        return a_Effect != null ? a_Effect.Duration : 0f;
    } //private static float GetDuration()

    //Runtime Effect가 있으면 Runtime Knockback 수치를 우선 반환
    private static float GetKnockbackPower(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.KnockbackPower;

        return a_Effect != null ? a_Effect.KnockbackPower : 0f;
    } //private static float GetKnockbackPower()

    //Runtime Effect가 있으면 Runtime 이동 거리를 우선 반환
    private static float GetMoveDistance(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.MoveDistance;

        return a_Effect != null ? a_Effect.MoveDistance : 0f;
    } //private static float GetMoveDistance()

    //Runtime Effect가 있으면 Runtime 이동시간을 우선 반환
    private static float GetMoveDuration(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.MoveDuration;

        return a_Effect != null ? a_Effect.MoveDuration : 0f;
    } //private static float GetMoveDuration()

    //Runtime Effect가 있으면 Runtime 정지 거리를 우선 반환
    private static float GetStopDistance(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.StopDistance;

        return a_Effect != null ? a_Effect.StopDistance : 0f;
    } //private static float GetStopDistance()

    //Runtime Effect가 있으면 Runtime Pull 힘을 우선 반환
    private static float GetPullForce(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.PullForce;

        return a_Effect != null ? a_Effect.PullForce : 0f;
    } //private static float GetPullForce()

    //Special Executor에서 반응형 지뢰 설치 실제 적용 요청
    public static void ApplyReactiveMineFromSpecialExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyReactiveMine(
            a_Effect,
            a_Context,
            a_RuntimeEffect
        );
    } //public static void ApplyReactiveMineFromSpecialExecutor()

    //Runtime Effect가 있으면 Runtime Pull 최대속도를 우선 반환
    private static float GetPullMaxSpeed(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.PullMaxSpeed;

        return a_Effect != null ? a_Effect.PullMaxSpeed : 0f;
    } //private static float GetPullMaxSpeed()

    //Runtime Effect가 있으면 Runtime DamageType을 우선 반환
    private static DamageType GetDamageType(
        ActionEffectInfo a_Effect,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        if (a_RuntimeEffect != null)
            return a_RuntimeEffect.DamageType;

        return a_Effect != null ? a_Effect.DamageType : default;
    } //private static DamageType GetDamageType()

    //특수 Effect를 Special Executor로 전달
    private static bool ApplySpecialEffect(
        ActionEffectType a_EffectType,
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        return ActionSpecialEffectExecutor.Apply(
            a_EffectType,
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //private static bool ApplySpecialEffect()

    //수치 기반 Effect를 Value Executor로 전달
    private static bool ApplyValueEffect(
        ActionEffectType a_EffectType,
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        return ActionValueEffectExecutor.Apply(
            a_EffectType,
            a_Effect,
            a_Context,
            a_Target,
            a_ImpactPosition,
            a_RuntimeEffect
        );
    } //private static bool ApplyValueEffect()

    //이동 기반 Effect를 Movement Executor로 전달
    private static bool ApplyMovementEffect(
        ActionEffectType a_EffectType,
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        return ActionMovementEffectExecutor.Apply(
            a_EffectType,
            a_Effect,
            a_Context,
            a_Target,
            a_ImpactPosition,
            a_RuntimeEffect
        );
    } //private static bool ApplyMovementEffect()

    //상태 및 방어 Effect를 StatusDefense Executor로 전달
    private static bool ApplyStatusOrDefenseEffect(
        ActionEffectType a_EffectType,
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        return ActionStatusDefenseEffectExecutor.Apply(
            a_EffectType,
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //private static bool ApplyStatusOrDefenseEffect()

    //Value Executor에서 회복 Effect 실제 적용 요청
    public static void ApplyHealFromValueExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyHeal(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void ApplyHealFromValueExecutor()

    //Value Executor에서 초과 회복 Shield Effect 실제 적용 요청
    public static void ApplyHealWithOverhealShieldFromValueExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyHealWithOverhealShield(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void ApplyHealWithOverhealShieldFromValueExecutor()

    //Value Executor에서 피해 Effect 실제 적용 요청
    public static void ApplyDamageFromValueExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyDamage(
            a_Effect,
            a_Context,
            a_Target,
            a_ImpactPosition,
            a_RuntimeEffect
        );
    } //public static void ApplyDamageFromValueExecutor()

    //Movement Executor에서 시전자 Dash 실제 적용 요청
    public static void ApplyDashCasterFromMovementExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyDashCaster(
            a_Effect,
            a_Context,
            a_RuntimeEffect
        );
    } //public static void ApplyDashCasterFromMovementExecutor()

    //Movement Executor에서 대상 Pull 실제 적용 요청
    public static void ApplyPullTargetFromMovementExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyPullTarget(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void ApplyPullTargetFromMovementExecutor()

    //Movement Executor에서 대상 후방 이동 실제 적용 요청
    public static void ApplyMoveBehindTargetFromMovementExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyMoveBehindTarget(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void ApplyMoveBehindTargetFromMovementExecutor()

    //StatusDefense Executor에서 부착형 Status Effect 실제 적용 요청
    public static void ApplyAttachedStatusFromStatusDefenseExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyAttachedStatus(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void ApplyAttachedStatusFromStatusDefenseExecutor()

    //StatusDefense Executor에서 보호막 Effect 실제 적용 요청
    public static void ApplyShieldFromStatusDefenseExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyShield(
            a_Effect,
            a_Context,
            a_Target,
            a_RuntimeEffect
        );
    } //public static void ApplyShieldFromStatusDefenseExecutor()

    //Special Executor에서 다음 기본공격 강화 실제 적용 요청
    public static void ApplyNextBasicAttackEnhancementFromSpecialExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context)
    {
        ApplyNextBasicAttackEnhancement(
            a_Effect,
            a_Context
        );
    } //public static void ApplyNextBasicAttackEnhancementFromSpecialExecutor()

    //Special Executor에서 벽 생성 Effect 실제 적용 요청
    public static void ApplyDeployWallFromSpecialExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context)
    {
        ApplyDeployWall(
            a_Effect,
            a_Context
        );
    } //public static void ApplyDeployWallFromSpecialExecutor()

    //Special Executor에서 Trigger Effect 실제 적용 요청
    public static void ApplyTriggerFromSpecialExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target)
    {
        ApplyTrigger(
            a_Effect,
            a_Context,
            a_Target
        );
    } //public static void ApplyTriggerFromSpecialExecutor()

    //Movement Executor에서 대상 Push 실제 적용 요청
    public static void ApplyPushTargetFromMovementExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context,
        ActionEffectTarget a_Target,
        Vector2 a_ImpactPosition,
        RuntimeActionEffectData a_RuntimeEffect)
    {
        ApplyPushTarget(
            a_Effect,
            a_Context,
            a_Target,
            a_ImpactPosition,
            a_RuntimeEffect
        );
    } //public static void ApplyPushTargetFromMovementExecutor()

    //Special Executor에서 Decoy 생성 실제 적용 요청
    public static void ApplyDeployDecoyFromSpecialExecutor(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context)
    {
        ApplyDeployDecoy(
            a_Effect,
            a_Context
        );
    } //public static void ApplyDeployDecoyFromSpecialExecutor()

    //시전자의 현재 위치에 전투용 Decoy 생성
    private static void ApplyDeployDecoy(
        ActionEffectInfo a_Effect,
        ActionUserContext a_Context)
    {
        if (a_Effect == null ||
            a_Context == null ||
            a_Context.GetCasterTransform() == null ||
            a_Effect.DecoyPrefab == null)
        {
            return;
        }

        Object.Instantiate(
            a_Effect.DecoyPrefab,
            a_Context.GetCasterPosition(),
            Quaternion.identity
        );
    } //private static void ApplyDeployDecoy()
} //public static class ActionEffectExecutor