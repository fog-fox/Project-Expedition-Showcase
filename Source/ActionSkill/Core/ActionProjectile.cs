using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class ActionProjectile : MonoBehaviour
{
    /*
    Action의 Projectile Delivery를 실행하며 이동, 수명, 충돌 반응,
    관통 처리, 비행 중 효과 적용과 Pool 반환을 관리하는 컴포넌트
    */

    private Rigidbody2D m_Rigidbody;

    private ActionDefinition m_Definition;
    private RuntimeSkillData m_RuntimeData;
    private ActionUserContext m_Context;

    private CombatTarget m_CasterCombatTarget;

    private Vector2 m_Direction;

    private float m_Speed;
    private float m_LifeTime;
    private float m_InitialLifeTime;

    private LayerMask m_TargetLayerMask;
    private LayerMask m_BlockLayerMask;

    private Vector3 m_BaseScale;

    private readonly HashSet<GameObject> m_PiercedTargetObjects =
        new HashSet<GameObject>();

    private readonly ActionProjectileGrowthController m_GrowthController =
        new ActionProjectileGrowthController();

    private readonly ActionProjectileCasterCollisionIgnore m_CasterCollisionIgnore =
        new ActionProjectileCasterCollisionIgnore();

    private readonly ActionProjectileActiveEffectApplier m_ActiveEffectApplier =
        new ActionProjectileActiveEffectApplier();

    private bool m_IsInitialized;
    private bool m_IsDeployed;
    private bool m_IsReleasing;

    public float CurrentRadius => m_GrowthController.GetCurrentRadius();

    //Projectile에서 사용할 Rigidbody와 Collider 정보 초기화
    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody2D>();
        m_BaseScale = transform.localScale;

        m_CasterCollisionIgnore.CacheProjectileColliders(gameObject);
    } //private void Awake()

    //Action Runtime 정보를 받아 Projectile 상태와 이동 효과 초기화
    public void Init(
        ActionDefinition a_Definition,
        RuntimeSkillData a_RuntimeData,
        ActionUserContext a_Context,
        Vector2 a_Direction,
        float a_Speed,
        float a_LifeTime,
        LayerMask a_TargetLayerMask,
        LayerMask a_BlockLayerMask)
    {
        if (m_Rigidbody == null)
            m_Rigidbody = GetComponent<Rigidbody2D>();

        m_IsReleasing = false;

        ClearRuntimeState();

        m_BaseScale = transform.localScale;

        m_Definition = a_Definition;
        m_RuntimeData = a_RuntimeData;
        m_Context = a_Context;

        if (m_Definition == null || m_RuntimeData == null || m_Context == null)
        {
            ReleaseToPool();
            return;
        }

        m_Direction = a_Direction.sqrMagnitude > 0.001f
            ? a_Direction.normalized
            : Vector2.right;

        m_Speed = Mathf.Max(0f, a_Speed);

        m_LifeTime = Mathf.Max(0.01f, a_LifeTime);
        m_InitialLifeTime = m_LifeTime;

        m_TargetLayerMask = a_TargetLayerMask;
        m_BlockLayerMask = a_BlockLayerMask;

        if (m_Context.m_CasterObject != null)
            m_CasterCombatTarget = CombatTargetRegistry.GetTarget(m_Context.m_CasterObject);

        m_GrowthController.Init(
            m_RuntimeData,
            transform,
            m_BaseScale
        );

        m_ActiveEffectApplier.Init(
            m_Definition,
            m_RuntimeData,
            m_Context,
            transform,
            m_Direction,
            m_TargetLayerMask
        );

        m_CasterCollisionIgnore.Apply(m_Context.m_CasterObject);

        m_IsDeployed = false;
        m_IsInitialized = true;

        UpdateRotation();
        NotifyInitializedHandlers();
    } //public void Init()

    //Rigidbody2D에 현재 Projectile 이동속도 적용
    private void FixedUpdate()
    {
        if (m_IsInitialized == false || m_IsDeployed || m_IsReleasing)
            return;

        if (m_Rigidbody == null)
            return;

        m_Rigidbody.linearVelocity = m_Direction * m_Speed;
    } //private void FixedUpdate()

    //Projectile 수명, 성장 및 비행 중 Effect 상태 갱신
    private void Update()
    {
        if (m_IsInitialized == false || m_IsDeployed || m_IsReleasing)
            return;

        m_LifeTime = Mathf.Max(0f, m_LifeTime - Time.deltaTime);

        m_GrowthController.Tick(
            m_LifeTime,
            m_InitialLifeTime
        );

        m_ActiveEffectApplier.Tick(
            Time.deltaTime,
            CurrentRadius
        );

        if (m_LifeTime > 0f)
            return;

        if (m_Definition != null && m_Definition.DeployOnLifeTimeEnd)
        {
            Deploy(null);
            return;
        }

        ReleaseToPool();
    } //private void Update()

    //Projectile가 Collider에 진입했을 때 유효 Target 또는 Block Layer에 따른 충돌 응답 처리
    private void OnTriggerEnter2D(Collider2D a_Collision)
    {
        if (m_IsInitialized == false || m_IsDeployed || m_IsReleasing)
            return;

        if (a_Collision == null || IsCasterCollision(a_Collision))
            return;

        if (IsTargetLayer(a_Collision))
        {
            CombatTarget combatTarget = CombatTargetRegistry.GetTarget(a_Collision);

            if (combatTarget != null)
            {
                if (IsValidTargetCollision(combatTarget))
                {
                    HandleCollisionResponse(
                        m_Definition != null
                            ? m_Definition.TargetHitResponse
                            : ActionProjectileHitResponse.Destroy,
                        a_Collision
                    );

                    return;
                }

                /*
                Target Layer에 속하지만 현재 Action TargetFilter와 맞지 않는
                아군 등의 CombatTarget은 Projectile 충돌 대상으로 처리하지 않습니다.
                */
                return;
            }
        }

        if (IsBlockLayer(a_Collision))
        {
            HandleCollisionResponse(
                m_Definition != null
                    ? m_Definition.BlockHitResponse
                    : ActionProjectileHitResponse.Destroy,
                a_Collision
            );
        }
    } //private void OnTriggerEnter2D()

    //충돌한 CombatTarget이 현재 Action의 TargetFilter와 일치하는지 확인
    private bool IsValidTargetCollision(CombatTarget a_Target)
    {
        if (a_Target == null ||
            a_Target.RootObject == null ||
            m_Context == null ||
            m_RuntimeData == null)
        {
            return false;
        }

        return ActionTargetResolver.TryGetValidTargetObject(
            m_Context.m_CasterObject,
            a_Target.RootObject,
            m_RuntimeData.TargetFilter,
            out _
        );
    } //private bool IsValidTargetCollision()

    //설정된 Projectile Hit Response에 맞는 충돌 동작 실행
    private void HandleCollisionResponse(
        ActionProjectileHitResponse a_Response,
        Collider2D a_Collision)
    {
        switch (a_Response)
        {
            case ActionProjectileHitResponse.Destroy:
                ReleaseToPool();
                return;

            case ActionProjectileHitResponse.Deploy:
                Deploy(GetCollisionObject(a_Collision));
                return;

            case ActionProjectileHitResponse.Pierce:
                ExecutePierceImpact(a_Collision);
                return;

            case ActionProjectileHitResponse.Ignore:
                return;
        }
    } //private void HandleCollisionResponse()

    //관통한 대상을 한 번만 처리하고 해당 위치에 Impact 실행
    private void ExecutePierceImpact(Collider2D a_Collision)
    {
        if (m_Definition == null || a_Collision == null)
            return;

        CombatTarget combatTarget = CombatTargetRegistry.GetTarget(a_Collision);

        if (combatTarget == null)
            return;

        GameObject targetObject = combatTarget.RootObject;

        if (targetObject == null || m_PiercedTargetObjects.Contains(targetObject))
            return;

        m_PiercedTargetObjects.Add(targetObject);

        m_Definition.ExecuteImpact(
            m_Context,
            m_RuntimeData,
            transform.position,
            targetObject
        );
    } //private void ExecutePierceImpact()

    //현재 위치에서 Projectile Impact를 실행하고 Projectile 종료
    private void Deploy(GameObject a_HitObject)
    {
        if (m_IsDeployed || m_IsReleasing)
            return;

        m_IsDeployed = true;

        StopRigidbody();

        if (m_Definition != null)
        {
            m_Definition.ExecuteImpact(
                m_Context,
                m_RuntimeData,
                transform.position,
                a_HitObject
            );
        }

        NotifyDeployHandlers(a_HitObject);

        ReleaseToPool();
    } //private void Deploy()

    //충돌 Collider가 CombatTarget이면 Root Object를 우선 반환
    private GameObject GetCollisionObject(Collider2D a_Collision)
    {
        if (a_Collision == null)
            return null;

        CombatTarget combatTarget = CombatTargetRegistry.GetTarget(a_Collision);

        if (combatTarget != null && combatTarget.RootObject != null)
            return combatTarget.RootObject;

        return a_Collision.gameObject;
    } //private GameObject GetCollisionObject()

    //Projectile Rigidbody의 모든 이동 정지
    private void StopRigidbody()
    {
        if (m_Rigidbody == null)
            return;

        m_Rigidbody.linearVelocity = Vector2.zero;
        m_Rigidbody.angularVelocity = 0f;
    } //private void StopRigidbody()

    //이전 Projectile 실행에서 사용한 모든 Runtime 상태 초기화
    private void ClearRuntimeState()
    {
        m_CasterCollisionIgnore.Clear();

        m_GrowthController.Reset();
        m_ActiveEffectApplier.Clear();

        m_PiercedTargetObjects.Clear();

        m_Definition = null;
        m_RuntimeData = null;
        m_Context = null;
        m_CasterCombatTarget = null;

        m_Direction = Vector2.right;

        m_Speed = 0f;

        m_LifeTime = 0f;
        m_InitialLifeTime = 0f;

        m_TargetLayerMask = default;
        m_BlockLayerMask = default;

        m_IsInitialized = false;
        m_IsDeployed = false;

        StopRigidbody();
    } //private void ClearRuntimeState()

    //Projectile Lifecycle 종료를 알리고 Object Pool로 반환
    private void ReleaseToPool()
    {
        if (m_IsReleasing)
            return;

        m_IsReleasing = true;

        if (m_IsInitialized)
            NotifyReleasedHandlers();

        ClearRuntimeState();

        if (ActionObjectPool.Release(gameObject))
            return;

        Destroy(gameObject);
    } //private void ReleaseToPool()

    //Projectile 진행 방향에 맞춰 Transform 회전 갱신
    private void UpdateRotation()
    {
        float angle = Mathf.Atan2(m_Direction.y, m_Direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            angle
        );
    } //private void UpdateRotation()

    //충돌한 Collider가 Projectile 시전자에게 속하는지 확인
    private bool IsCasterCollision(Collider2D a_Collision)
    {
        if (m_Context == null ||
            m_Context.m_CasterObject == null ||
            a_Collision == null)
        {
            return false;
        }

        CombatTarget collisionTarget = CombatTargetRegistry.GetTarget(a_Collision);

        if (m_CasterCombatTarget != null && collisionTarget != null)
            return m_CasterCombatTarget == collisionTarget;

        if (a_Collision.gameObject == m_Context.m_CasterObject)
            return true;

        return a_Collision.transform.IsChildOf(m_Context.m_CasterObject.transform);
    } //private bool IsCasterCollision()

    //충돌 Collider가 Action의 Target Layer에 포함되는지 확인
    private bool IsTargetLayer(Collider2D a_Collision)
    {
        if (a_Collision == null)
            return false;

        if (m_TargetLayerMask.value == 0)
            return true;

        int collisionLayerMask = 1 << a_Collision.gameObject.layer;

        return (m_TargetLayerMask.value & collisionLayerMask) != 0;
    } //private bool IsTargetLayer()

    //충돌 Collider가 Projectile을 막는 Block Layer에 포함되는지 확인
    private bool IsBlockLayer(Collider2D a_Collision)
    {
        if (a_Collision == null || m_BlockLayerMask.value == 0)
            return false;

        int collisionLayerMask = 1 << a_Collision.gameObject.layer;

        return (m_BlockLayerMask.value & collisionLayerMask) != 0;
    } //private bool IsBlockLayer()

    //Deploy 처리에 관심 있는 Projectile Handler들에게 결과 전달
    private void NotifyDeployHandlers(GameObject a_HitObject)
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IActionProjectileDeployHandler deployHandler)
                continue;

            deployHandler.OnProjectileDeployed(
                m_Definition,
                m_RuntimeData,
                m_Context,
                transform.position,
                m_Direction,
                a_HitObject
            );
        }
    } //private void NotifyDeployHandlers()

    //Projectile 초기화 완료를 Lifecycle Handler들에게 전달
    private void NotifyInitializedHandlers()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IActionProjectileLifecycleHandler handler)
                continue;

            handler.OnProjectileInitialized(
                m_Definition,
                m_RuntimeData,
                m_Context
            );
        }
    } //private void NotifyInitializedHandlers()

    //Projectile 종료를 Lifecycle Handler들에게 전달
    private void NotifyReleasedHandlers()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IActionProjectileLifecycleHandler handler)
                continue;

            handler.OnProjectileReleased();
        }
    } //private void NotifyReleasedHandlers()

    //Pool 외부에서 직접 제거된 경우 Runtime 보조 상태 정리
    private void OnDestroy()
    {
        if (m_IsInitialized && m_IsReleasing == false)
            NotifyReleasedHandlers();

        m_CasterCollisionIgnore.Clear();
        m_ActiveEffectApplier.Clear();
    } //private void OnDestroy()
} //public class ActionProjectile