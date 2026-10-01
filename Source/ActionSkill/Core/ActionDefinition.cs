using UnityEngine;

[CreateAssetMenu(
    menuName = "Last Expedition/Action/Action Definition",
    fileName = "New Action Definition"
)]
public class ActionDefinition : ScriptableObject
{
    /*
    Action의 전달 방식, 대상 및 범위, 투사체, Effect와 조준 설정을 정의하고
    Runtime Action 실행과 Impact 생성을 관리하는 ScriptableObject
    */

    [Header("Base")]
    [SerializeField] private string m_ActionName = "New Action";

    [Header("Delivery")]
    [SerializeField] private ActionDeliveryType m_DeliveryType = ActionDeliveryType.Instant;

    [Header("Projectile")]
    [SerializeField] private ActionProjectile m_ProjectilePrefab;
    [SerializeField] private float m_ProjectileSpeed = 8f;
    [SerializeField] private float m_ProjectileLifeTime = 2.5f;
    [SerializeField] private float m_ProjectileSpawnDistance = 0.45f;

    [Header("Caster Recoil")]
    [Tooltip("활성화하면 Projectile 발사 성공 시 시전자를 사용 방향 반대로 밀어냅니다.")]
    [SerializeField] private bool m_UseCasterRecoil;

    [SerializeField, Min(0f)] private float m_CasterRecoilDistance = 0.5f;
    [SerializeField, Min(0.01f)] private float m_CasterRecoilDuration = 0.08f;

    [Header("Projectile Pattern")]
    [Tooltip(
        "한 번의 Action 실행으로 생성되는 투사체 수.\n" +
        "1이면 단일 투사체 방식으로 작동."
    )]
    [SerializeField, Min(1)] private int m_ProjectileCount = 1;

    [Tooltip(
        "생성되는 모든 투사체가 차지하는 전체 부채꼴 각도.\n" +
        "예: 투사체 3개, 각도 40이면 -20, 0, +20도로 발사."
    )]
    [SerializeField, Range(0f, 180f)] private float m_ProjectileSpreadAngle;

    [Header("Projectile Growth")]
    [Tooltip("활성화 시 투사체 수명 진행에 따라 시작 크기에서 최종 크기로 변화.")]
    [SerializeField] private bool m_GrowProjectileWhileMoving;

    [Tooltip("투사체 생성 직후 원본 크기에 대한 비율.")]
    [SerializeField, Range(0.01f, 3f)] private float m_ProjectileStartScaleRatio = 1f;

    [Tooltip("투사체 수명 종료 직전 원본 크기에 대한 비율.")]
    [SerializeField, Range(0.01f, 3f)] private float m_ProjectileEndScaleRatio = 1f;

    [Tooltip("투사체 수명 진행률에 따른 성장 곡선.")]
    [SerializeField]
    private AnimationCurve m_ProjectileGrowthCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Projectile Hit Response")]
    [SerializeField]
    private ActionProjectileHitResponse m_TargetHitResponse =
        ActionProjectileHitResponse.Deploy;

    [SerializeField]
    private ActionProjectileHitResponse m_BlockHitResponse =
        ActionProjectileHitResponse.Deploy;

    [Header("Projectile Lifetime Response")]
    [SerializeField] private bool m_DeployOnLifeTimeEnd = true;

    [Header("Projectile Active Effect")]
    [Tooltip("활성화 시 이동 중인 투사체 범위 내 대상에게 Action Effect 적용.")]
    [SerializeField] private bool m_ApplyEffectsWhileProjectileActive;

    [Tooltip(
        "이동 중 투사체의 기본 대상 탐색 간격.\n" +
        "실제 Effect 적용 주기는 각 Effect의 Tick Interval을 따름."
    )]
    [SerializeField, Min(0.05f)] private float m_ProjectileEffectCheckInterval = 0.1f;

    [Tooltip(
        "투사체 프리팹이 원본 크기일 때의 실제 반경.\n" +
        "최종 크기를 Action Radius에 맞추기 위한 기준값."
    )]
    [SerializeField, Min(0.01f)] private float m_ProjectileVisualBaseRadius = 0.5f;

    [Header("Aim")]
    [SerializeField] private SkillAimPreviewType m_AimPreviewType = SkillAimPreviewType.RangeOnly;
    [SerializeField] private SkillAimRangeMode m_AimRangeMode = SkillAimRangeMode.Auto;
    [SerializeField]
    private SkillAimProjectileEndMode m_ProjectileAimEndMode =
        SkillAimProjectileEndMode.FullRange;

    [SerializeField, Min(0f)] private float m_CastRange = 5f;
    [SerializeField] private bool m_ClampAimToCastRange = true;

    [Header("Target")]
    [SerializeField] private ActionTargetMode m_TargetMode = ActionTargetMode.Caster;
    [SerializeField] private ActionTargetFilter m_TargetFilter = ActionTargetFilter.Self;
    [SerializeField] private float m_Radius = 2.5f;

    [Header("Area Shape")]
    [SerializeField] private ActionAreaShape m_AreaShape = ActionAreaShape.Circle;

    [Tooltip(
        "Sector 범위의 전체 각도.\n" +
        "시전 방향을 중심으로 좌우 절반씩 적용."
    )]
    [SerializeField, Range(1f, 360f)] private float m_SectorAngle = 90f;

    [Header("Area Behaviour")]
    [Tooltip(
        "CasterRadius 범위 효과의 시전자 추적 여부.\n" +
        "비활성화 시 시전 순간 위치에 고정."
    )]
    [SerializeField] private bool m_FollowCasterWhileActive = true;

    [Header("Layer Override")]
    [SerializeField] private LayerMask m_TargetLayerMaskOverride;
    [SerializeField] private LayerMask m_BlockLayerMaskOverride;

    [Header("Impact Visual")]
    [SerializeField] private GameObject m_ImpactVisualPrefab;

    [Header("Range Visual")]
    [SerializeField] private bool m_ShowRangeVisual;

    [SerializeField] private Color m_RangeFillColor = new Color(0.9f, 0.15f, 0.2f, 0.18f);
    [SerializeField] private Color m_RangeOutlineColor = new Color(1f, 0.35f, 0.35f, 0.85f);

    [SerializeField] private int m_RangeVisualSegments = 48;
    [SerializeField] private int m_RangeVisualSortingOrder = 10;

    [Header("State Variant")]
    [Tooltip(
    "Caster의 현재 Action State에 따라 기본 Target 또는 Effect를 대체하는 설정. " +
    "일치하는 State ID가 없으면 기본 Action 설정 사용."
)]
    [SerializeField] private ActionStateVariant[] m_StateVariants;

    [Header("Effects")]
    [SerializeField] private ActionEffectInfo[] m_Effects;

    private const int HitscanResultBufferSize = 32;

    private readonly RaycastHit2D[] m_HitscanResultBuffer =
        new RaycastHit2D[HitscanResultBufferSize];

    public string ActionName => m_ActionName;
    public ActionDeliveryType DeliveryType => m_DeliveryType;

    public ActionProjectile ProjectilePrefab => m_ProjectilePrefab;
    public float ProjectileSpeed => Mathf.Max(0f, m_ProjectileSpeed);
    public float ProjectileLifeTime => Mathf.Max(0.01f, m_ProjectileLifeTime);
    public float ProjectileSpawnDistance => Mathf.Max(0f, m_ProjectileSpawnDistance);
    public int ProjectileCount => Mathf.Max(1, m_ProjectileCount);
    public float ProjectileSpreadAngle => Mathf.Clamp(m_ProjectileSpreadAngle, 0f, 180f);

    public bool GrowProjectileWhileMoving => m_GrowProjectileWhileMoving;
    public float ProjectileStartScaleRatio => Mathf.Max(0.01f, m_ProjectileStartScaleRatio);
    public float ProjectileEndScaleRatio => Mathf.Max(0.01f, m_ProjectileEndScaleRatio);
    public AnimationCurve ProjectileGrowthCurve => m_ProjectileGrowthCurve;

    public ActionProjectileHitResponse TargetHitResponse => m_TargetHitResponse;
    public ActionProjectileHitResponse BlockHitResponse => m_BlockHitResponse;
    public bool DeployOnLifeTimeEnd => m_DeployOnLifeTimeEnd;

    public bool ApplyEffectsWhileProjectileActive => m_ApplyEffectsWhileProjectileActive;
    public float ProjectileEffectCheckInterval => Mathf.Max(0.05f, m_ProjectileEffectCheckInterval);
    public float ProjectileVisualBaseRadius => Mathf.Max(0.01f, m_ProjectileVisualBaseRadius);

    public SkillAimPreviewType AimPreviewType => m_AimPreviewType;
    public SkillAimRangeMode AimRangeMode => m_AimRangeMode;
    public SkillAimProjectileEndMode ProjectileAimEndMode => m_ProjectileAimEndMode;

    public float CastRange => Mathf.Max(0f, m_CastRange);
    public bool ClampAimToCastRange => m_ClampAimToCastRange;

    public ActionTargetMode TargetMode => m_TargetMode;
    public ActionTargetFilter TargetFilter => m_TargetFilter;
    public float Radius => Mathf.Max(0.1f, m_Radius);

    public ActionAreaShape AreaShape => m_AreaShape;
    public float SectorAngle => Mathf.Clamp(m_SectorAngle, 1f, 360f);
    public bool FollowCasterWhileActive => m_FollowCasterWhileActive;

    public bool ShowRangeVisual => m_ShowRangeVisual;
    public Color RangeFillColor => m_RangeFillColor;
    public Color RangeOutlineColor => m_RangeOutlineColor;
    public int RangeVisualSegments => Mathf.Clamp(m_RangeVisualSegments, 16, 128);
    public int RangeVisualSortingOrder => m_RangeVisualSortingOrder;
    public int StateVariantCount => m_StateVariants?.Length ?? 0;
    public ActionEffectInfo[] Effects => m_Effects;

    public bool UseCasterRecoil => m_UseCasterRecoil;
    public float CasterRecoilDistance => Mathf.Max(0f, m_CasterRecoilDistance);
    public float CasterRecoilDuration => Mathf.Max(0.01f, m_CasterRecoilDuration);

    //현재 Definition의 투사체 관련 설정을 Runtime 전달용 데이터로 생성
    public ActionProjectileSettings GetProjectileSettings()
    {
        return new ActionProjectileSettings(
            m_ProjectilePrefab,
            ProjectileSpeed,
            ProjectileLifeTime,
            ProjectileSpawnDistance,
            ProjectileCount,
            ProjectileSpreadAngle,
            GrowProjectileWhileMoving,
            ProjectileStartScaleRatio,
            ProjectileEndScaleRatio,
            ProjectileGrowthCurve,
            TargetHitResponse,
            BlockHitResponse,
            DeployOnLifeTimeEnd,
            ApplyEffectsWhileProjectileActive,
            ProjectileEffectCheckInterval,
            ProjectileVisualBaseRadius
        );
    } //public ActionProjectileSettings GetProjectileSettings()

    //현재 Definition의 범위 판정 설정을 Runtime 전달용 데이터로 생성
    public ActionAreaSettings GetAreaSettings()
    {
        return new ActionAreaSettings(
            TargetMode,
            TargetFilter,
            Radius,
            AreaShape,
            SectorAngle,
            FollowCasterWhileActive
        );
    } //public ActionAreaSettings GetAreaSettings()

    //지정 Index의 Action State Variant 반환
    public ActionStateVariant GetStateVariant(int a_Index)
    {
        if (m_StateVariants == null || a_Index < 0 || a_Index >= m_StateVariants.Length)
            return null;

        return m_StateVariants[a_Index];
    } //public ActionStateVariant GetStateVariant()

    //지정 Action State ID와 일치하는 Variant 탐색
    public bool TryGetStateVariant(string a_StateId, out ActionStateVariant a_Variant)
    {
        a_Variant = null;

        if (string.IsNullOrWhiteSpace(a_StateId) || m_StateVariants == null)
            return false;

        for (int i = 0; i < m_StateVariants.Length; i++)
        {
            ActionStateVariant variant = m_StateVariants[i];

            if (variant == null || variant.IsMatched(a_StateId) == false)
                continue;

            a_Variant = variant;
            return true;
        }

        return false;
    } //public bool TryGetStateVariant()

    //현재 Definition의 범위 시각화 설정을 Runtime 전달용 데이터로 생성
    public ActionRangeVisualSettings GetRangeVisualSettings()
    {
        return new ActionRangeVisualSettings(
            ShowRangeVisual,
            RangeFillColor,
            RangeOutlineColor,
            RangeVisualSegments,
            RangeVisualSortingOrder
        );
    } //public ActionRangeVisualSettings GetRangeVisualSettings()

    //Context를 기반으로 Runtime 데이터를 생성하여 Action 실행
    public ItemUseResult Execute(ActionUserContext a_Context)
    {
        RuntimeSkillData runtimeData = RuntimeSkillData.Create(this, a_Context);

        if (runtimeData == null)
            return ItemUseResult.Fail("런타임 액션 데이터 생성에 실패했습니다.");

        return Execute(
            a_Context,
            runtimeData
        );
    } //public ItemUseResult Execute()

    //전달받은 Runtime 데이터를 사용하여 Action 검증 및 실행
    public ItemUseResult Execute(
        ActionUserContext a_Context,
        RuntimeSkillData a_RuntimeData)
    {
        if (a_Context == null)
            return ItemUseResult.Fail("액션 실행 정보가 없습니다.");

        if (a_RuntimeData == null)
            a_RuntimeData = RuntimeSkillData.Create(this, a_Context);

        if (a_RuntimeData == null)
            return ItemUseResult.Fail("런타임 액션 데이터 생성에 실패했습니다.");

        ItemUseResult validationResult = ValidateExecution(a_Context, a_RuntimeData);

        if (validationResult.m_IsSuccess == false)
            return validationResult;

        switch (m_DeliveryType)
        {
            case ActionDeliveryType.Instant:
                ExecuteInstant(
                    a_Context,
                    a_RuntimeData
                );

                return ItemUseResult.Success(true);

            case ActionDeliveryType.Projectile:
                return ExecuteProjectile(
                    a_Context,
                    a_RuntimeData
                );

            case ActionDeliveryType.Hitscan:
                return ExecuteHitscan(
                    a_Context,
                    a_RuntimeData
                );
        }

        return ItemUseResult.Fail("지원하지 않는 액션 전달 방식입니다.");
    } //public ItemUseResult Execute()

    //현재 Action State를 반영한 Target Filter 반환
    public ActionTargetFilter ResolveTargetFilter(string a_StateId)
    {
        ActionTargetFilter targetFilter = m_TargetFilter;

        if (string.IsNullOrWhiteSpace(a_StateId))
            return targetFilter;

        if (TryGetStateVariant(a_StateId, out ActionStateVariant variant) == false)
            return targetFilter;

        if (variant.OverrideTargetFilter)
            targetFilter = variant.TargetFilter;

        return targetFilter;
    }

    //현재 사용 방향으로 Hitscan을 실행하여 최초 유효 대상에 Impact 적용
    private ItemUseResult ExecuteHitscan(
        ActionUserContext a_Context,
        RuntimeSkillData a_RuntimeData)
    {
        if (a_Context.m_CasterTransform == null)
            return ItemUseResult.Fail("Hitscan 시작 위치가 없습니다.");

        Vector2 direction = a_Context.m_UseDirection;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.right;

        direction.Normalize();

        float range = CastRange;

        if (range <= 0f)
            return ItemUseResult.Fail("Hitscan 사거리가 0입니다.");

        int targetLayerMask = a_RuntimeData.TargetLayerMask.value;

        if (targetLayerMask == 0)
            targetLayerMask = Physics2D.AllLayers;

        int blockLayerMask = GetBlockLayerMask(a_Context).value;
        int raycastLayerMask = targetLayerMask | blockLayerMask;

        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(raycastLayerMask);
        contactFilter.useTriggers = Physics2D.queriesHitTriggers;

        int hitCount = Physics2D.Raycast(
            a_Context.m_CasterTransform.position,
            direction,
            contactFilter,
            m_HitscanResultBuffer,
            range
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = m_HitscanResultBuffer[i];
            m_HitscanResultBuffer[i] = default;

            Collider2D hitCollider = hit.collider;

            if (hitCollider == null)
                continue;

            if (IsCasterCollider(a_Context, hitCollider))
                continue;

            GameObject hitObject = hitCollider.gameObject;
            int hitLayer = 1 << hitObject.layer;

            bool isTargetLayer =
                (targetLayerMask & hitLayer) != 0;

            if (isTargetLayer &&
                ActionTargetResolver.TryGetValidTargetObject(
                    a_Context.m_CasterObject,
                    hitObject,
                    a_RuntimeData.TargetFilter,
                    out GameObject resolvedTargetObject))
            {
                ExecuteImpact(
                    a_Context,
                    a_RuntimeData,
                    hit.point,
                    resolvedTargetObject
                );

                ClearHitscanResultBuffer(hitCount);

                return ItemUseResult.Success(true);
            }

            if ((blockLayerMask & hitLayer) != 0)
            {
                ClearHitscanResultBuffer(hitCount);
                return ItemUseResult.Success(true);
            }
        }

        ClearHitscanResultBuffer(hitCount);

        return ItemUseResult.Success(true);
    } //private ItemUseResult ExecuteHitscan()

    //Hitscan 충돌 대상이 현재 시전자 자신의 Collider인지 확인
    private bool IsCasterCollider(
        ActionUserContext a_Context,
        Collider2D a_Collider)
    {
        if (a_Context == null ||
            a_Context.m_CasterTransform == null ||
            a_Collider == null)
        {
            return false;
        }

        Transform hitTransform = a_Collider.transform;

        if (hitTransform == a_Context.m_CasterTransform ||
            hitTransform.IsChildOf(a_Context.m_CasterTransform))
        {
            return true;
        }

        CombatTarget casterTarget =
            CombatTargetRegistry.GetTarget(a_Context.m_CasterObject);

        CombatTarget hitTarget =
            CombatTargetRegistry.GetTarget(a_Collider);

        return casterTarget != null &&
               hitTarget != null &&
               casterTarget == hitTarget;
    } //private bool IsCasterCollider()

    //사용한 Hitscan 결과 Buffer 정리
    private void ClearHitscanResultBuffer(int a_HitCount)
    {
        int count = Mathf.Min(
            Mathf.Max(0, a_HitCount),
            m_HitscanResultBuffer.Length
        );

        for (int i = 0; i < count; i++)
            m_HitscanResultBuffer[i] = default;
    } //private void ClearHitscanResultBuffer()

    //기본 Layer와 현재 Action State를 반영한 최종 Target LayerMask 반환
    public LayerMask ResolveTargetLayerMask(
        LayerMask a_DefaultTargetLayerMask,
        string a_StateId)
    {
        LayerMask targetLayerMask =
            m_TargetLayerMaskOverride.value != 0
                ? m_TargetLayerMaskOverride
                : a_DefaultTargetLayerMask;

        if (string.IsNullOrWhiteSpace(a_StateId))
            return targetLayerMask;

        if (TryGetStateVariant(a_StateId, out ActionStateVariant variant) == false)
            return targetLayerMask;

        if (variant.OverrideTargetLayerMask)
            targetLayerMask = variant.TargetLayerMask;

        return targetLayerMask;
    }

    //즉시 실행 Action의 Impact 위치를 계산하여 Impact 실행
    private void ExecuteInstant(
        ActionUserContext a_Context,
        RuntimeSkillData a_RuntimeData)
    {
        Vector2 impactPosition = GetInstantImpactPosition(
            a_Context,
            a_RuntimeData.TargetMode
        );

        ExecuteImpact(
            a_Context,
            a_RuntimeData,
            impactPosition,
            a_Context.m_HitObject
        );
    } //private void ExecuteInstant()

    //Runtime 투사체 설정을 기반으로 하나 이상의 투사체 생성
    private ItemUseResult ExecuteProjectile(
        ActionUserContext a_Context,
        RuntimeSkillData a_RuntimeData)
    {
        ActionProjectileSettings projectileSettings = GetProjectileSettings();

        if (projectileSettings.m_ProjectilePrefab == null)
            return ItemUseResult.Fail("액션 투사체 프리팹이 없습니다.");

        if (a_Context.m_CasterTransform == null)
            return ItemUseResult.Fail("투사체 생성 위치가 없습니다.");

        Vector2 baseDirection = a_Context.m_UseDirection;

        if (baseDirection.sqrMagnitude <= 0.001f)
            baseDirection = Vector2.right;

        baseDirection.Normalize();

        int projectileCount = Mathf.Max(1, a_RuntimeData.ProjectileCount);

        Vector2 spawnPosition =
            (Vector2)a_Context.m_CasterTransform.position +
            baseDirection * a_RuntimeData.ProjectileSpawnDistance;

        int spawnedProjectileCount = 0;

        for (int i = 0; i < projectileCount; i++)
        {
            float angleOffset = ActionProjectileSettings.GetAngleOffset(
                i,
                projectileCount,
                a_RuntimeData.ProjectileSpreadAngle
            );

            Vector2 projectileDirection =
                ActionProjectileSettings.RotateDirection(
                    baseDirection,
                    angleOffset
                );

            ActionProjectile projectile =
                ActionObjectPool.SpawnProjectile(
                    projectileSettings.m_ProjectilePrefab,
                    spawnPosition,
                    Quaternion.identity
                );

            if (projectile == null)
                continue;

            projectile.Init(
                this,
                a_RuntimeData,
                a_Context,
                projectileDirection,
                a_RuntimeData.ProjectileSpeed,
                a_RuntimeData.ProjectileLifeTime,
                a_RuntimeData.TargetLayerMask,
                GetBlockLayerMask(a_Context)
            );

            spawnedProjectileCount++;
        }

        if (spawnedProjectileCount <= 0)
            return ItemUseResult.Fail("액션 투사체 풀 생성에 실패했습니다.");

        ApplyCasterRecoil(a_Context);

        return ItemUseResult.Success(true);
    } //private ItemUseResult ExecuteProjectile()

      //Runtime Target Mode를 기준으로 Instant Action의 Impact 위치 계산
    private Vector2 GetInstantImpactPosition(
        ActionUserContext a_Context,
        ActionTargetMode a_TargetMode)
    {
        switch (a_TargetMode)
        {
            case ActionTargetMode.Caster:
            case ActionTargetMode.CasterRadius:
                return a_Context.GetCasterPosition();

            case ActionTargetMode.HitObject:
                return a_Context.m_HitObject != null
                    ? (Vector2)a_Context.m_HitObject.transform.position
                    : a_Context.m_MouseWorldPosition;

            case ActionTargetMode.ImpactRadius:
                return a_Context.m_MouseWorldPosition;
        }

        return a_Context.GetCasterPosition();
    } //private Vector2 GetInstantImpactPosition()

    //새 Runtime 데이터를 생성하여 지정된 위치에서 Impact 실행
    public void ExecuteImpact(
        ActionUserContext a_Context,
        Vector2 a_ImpactPosition,
        GameObject a_HitObject)
    {
        if (a_Context == null)
            return;

        RuntimeSkillData runtimeData = RuntimeSkillData.Create(this, a_Context);

        if (runtimeData == null)
        {
            Debug.LogWarning($"{m_ActionName}: RuntimeSkillData 생성에 실패해 Impact를 실행할 수 없습니다.");
            return;
        }

        ExecuteImpact(
            a_Context,
            runtimeData,
            a_ImpactPosition,
            a_HitObject
        );
    } //public void ExecuteImpact()

    //지정된 Runtime 데이터로 Impact 오브젝트와 Runner 생성 및 초기화
    public void ExecuteImpact(
        ActionUserContext a_Context,
        RuntimeSkillData a_RuntimeData,
        Vector2 a_ImpactPosition,
        GameObject a_HitObject)
    {
        if (a_Context == null || a_RuntimeData == null)
            return;

        GameObject impactObject = ActionObjectPool.SpawnImpact(
            this,
            m_ImpactVisualPrefab,
            a_ImpactPosition,
            Quaternion.identity
        );

        if (impactObject == null)
            return;

        ActionImpactRunner runner = impactObject.GetComponent<ActionImpactRunner>();

        if (runner == null)
            runner = impactObject.AddComponent<ActionImpactRunner>();

        runner.Init(
            this,
            a_RuntimeData,
            a_Context,
            a_ImpactPosition,
            a_HitObject
        );
    } //public void ExecuteImpact()

    //Action 전용 Override가 있으면 우선 사용하고 없으면 Context의 대상 Layer 반환
    public LayerMask GetTargetLayerMask(ActionUserContext a_Context)
    {
        if (m_TargetLayerMaskOverride.value != 0)
            return m_TargetLayerMaskOverride;

        return a_Context != null ? a_Context.GetTargetLayerMask() : default;
    } //public LayerMask GetTargetLayerMask()

    //Action 전용 Override가 있으면 우선 사용하고 없으면 Context의 차단 Layer 반환
    public LayerMask GetBlockLayerMask(ActionUserContext a_Context)
    {
        if (m_BlockLayerMaskOverride.value != 0)
            return m_BlockLayerMaskOverride;

        return a_Context != null ? a_Context.GetBlockLayerMask() : default;
    } //public LayerMask GetBlockLayerMask()

    //현재 Runtime 대상 조건과 Effect를 기준으로 Action 실행 가능 여부 검증
    public ItemUseResult ValidateExecution(
        ActionUserContext a_Context,
        RuntimeSkillData a_RuntimeData)
    {
        if (a_Context == null)
            return ItemUseResult.Fail("액션 실행 정보가 없습니다.");

        if (a_RuntimeData == null)
            return ItemUseResult.Fail("런타임 액션 데이터가 없습니다.");

        if (m_DeliveryType == ActionDeliveryType.Projectile ||
            m_DeliveryType == ActionDeliveryType.Hitscan)
        {
            return ItemUseResult.Success(false);
        }

        GameObject validationTargetObject = null;

        switch (a_RuntimeData.TargetMode)
        {
            case ActionTargetMode.Caster:
                validationTargetObject = a_Context.GetCasterObject();
                break;

            case ActionTargetMode.HitObject:
                validationTargetObject = a_Context.m_HitObject;
                break;

            default:
                return ItemUseResult.Success(false);
        }

        if (ActionTargetResolver.TryResolveTargetObject(
                a_Context,
                validationTargetObject,
                a_RuntimeData.TargetFilter,
                out ActionEffectTarget target,
                out string targetFailMessage
            ) == false)
        {
            return ItemUseResult.Fail(targetFailMessage);
        }

        RuntimeActionEffectData[] runtimeEffects = a_RuntimeData.Effects;

        if (runtimeEffects == null || runtimeEffects.Length <= 0)
            return ItemUseResult.Success(false);

        for (int i = 0; i < runtimeEffects.Length; i++)
        {
            RuntimeActionEffectData effect = runtimeEffects[i];

            if (effect == null) continue;

            if (ActionEffectExecutor.CanApply(
                    effect,
                    a_Context,
                    target,
                    out string effectFailMessage
                ))
            {
                continue;
            }

            return ItemUseResult.Fail(effectFailMessage);
        }

        return ItemUseResult.Success(false);
    } //public ItemUseResult ValidateExecution()

    //Aim Range 설정 방식에 따라 실제 조준 가능 거리 계산
    public float ResolveAimRange()
    {
        if (m_AimRangeMode == SkillAimRangeMode.Manual)
            return CastRange;

        if (DeliveryType == ActionDeliveryType.Projectile)
            return ProjectileSpawnDistance + ProjectileSpeed * ProjectileLifeTime;

        return CastRange;
    } //public float ResolveAimRange()

    //Projectile 발사 시 설정된 시전자 반동 이동 실행
    private void ApplyCasterRecoil(ActionUserContext a_Context)
    {
        if (m_UseCasterRecoil == false ||
            a_Context == null ||
            a_Context.GetCasterObject() == null)
        {
            return;
        }

        if (CasterRecoilDistance <= 0f)
            return;

        ActionMovementRunner movementRunner =
            ActionMovementRunner.GetOrCreate(
                a_Context.GetCasterObject()
            );

        if (movementRunner == null)
            return;

        Vector2 recoilDirection = -a_Context.GetUseDirection();

        if (recoilDirection.sqrMagnitude <= 0.001f)
            return;

        movementRunner.StartDash(
            a_Context,
            recoilDirection,
            CasterRecoilDistance,
            CasterRecoilDuration
        );
    } //private void ApplyCasterRecoil()

#if UNITY_EDITOR
    //Inspector에서 변경된 설정값을 유효 범위로 보정
    private void OnValidate()
    {
        m_Radius = Mathf.Max(0.1f, m_Radius);
        m_SectorAngle = Mathf.Clamp(m_SectorAngle, 1f, 360f);

        m_ProjectileSpeed = Mathf.Max(0f, m_ProjectileSpeed);
        m_ProjectileLifeTime = Mathf.Max(0.01f, m_ProjectileLifeTime);
        m_ProjectileSpawnDistance = Mathf.Max(0f, m_ProjectileSpawnDistance);

        m_ProjectileCount = Mathf.Max(1, m_ProjectileCount);
        m_ProjectileSpreadAngle = Mathf.Clamp(m_ProjectileSpreadAngle, 0f, 180f);

        m_ProjectileStartScaleRatio = Mathf.Max(0.01f, m_ProjectileStartScaleRatio);
        m_ProjectileEndScaleRatio = Mathf.Max(0.01f, m_ProjectileEndScaleRatio);

        if (m_ProjectileGrowthCurve == null || m_ProjectileGrowthCurve.length == 0)
            m_ProjectileGrowthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        m_CastRange = Mathf.Max(0f, m_CastRange);

        m_RangeVisualSegments = Mathf.Clamp(m_RangeVisualSegments, 16, 128);

        m_ProjectileEffectCheckInterval = Mathf.Max(0.05f, m_ProjectileEffectCheckInterval);
        m_ProjectileVisualBaseRadius = Mathf.Max(0.01f, m_ProjectileVisualBaseRadius);
    } //private void OnValidate()
#endif
} //public class ActionDefinition