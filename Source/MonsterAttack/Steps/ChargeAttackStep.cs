using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ChargeAttackStep",
    menuName = "Monster/Attack/Step/Charge"
)]
public class ChargeAttackStep : MonsterAttackStep
{
    /*
    공격 시작 시 Target 방향을 고정하고 Warning을 표시한 뒤
    해당 방향으로 돌진하며 이동 경로의 유효 Target에게 피해를 적용
    */

    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumChargeSpeed = 0.01f;
    private const float ObstacleSkinWidth = 0.01f;
    private const string DamageSourceName = "Charge";

    [Header("Charge")]
    [SerializeField, Min(0f)] private float m_Distance = 5f;
    [SerializeField, Min(MinimumChargeSpeed)] private float m_Speed = 10f;
    [SerializeField, Min(0f)] private float m_HitRadius = 0.5f;
    [SerializeField, Min(0f)] private float m_WarningTime = 0.8f;
    [SerializeField, Min(0f)] private float m_DamageMultiplier = 1f;

    [Header("Obstacle")]
    [SerializeField] private LayerMask m_ObstacleLayerMask;

    //Warning 후 고정된 방향으로 Charge Attack 전체 과정 실행
    public override IEnumerator Execute(MonsterAttackContext a_Context)
    {
        if (a_Context == null)
            yield break;

        Rigidbody2D rigidbody = a_Context.Rigidbody;

        if (rigidbody == null)
            yield break;

        Vector2 chargeOrigin = rigidbody.position;

        Vector2 chargeDirection =
            a_Context.GetDirectionToTarget(chargeOrigin);

        if (chargeDirection.sqrMagnitude <= MinimumDirectionMagnitude)
            yield break;

        chargeDirection.Normalize();

        float distance = Mathf.Max(0f, m_Distance);
        float speed = Mathf.Max(MinimumChargeSpeed, m_Speed);
        float radius = Mathf.Max(0f, m_HitRadius);
        float warningTime = Mathf.Max(0f, m_WarningTime);
        float damageMultiplier = Mathf.Max(0f, m_DamageMultiplier);

        /*
        이 Step이 시작되는 순간 Charge 방향을 고정합니다.

        같은 공격 Sequence에서 Charge Step을 여러 번 사용하더라도
        각 Step이 시작될 때마다 Target 위치를 새로 확인합니다.
        */
        yield return ShowWarning(
            a_Context,
            chargeOrigin,
            chargeDirection,
            distance,
            radius,
            warningTime
        );

        yield return ExecuteCharge(
            a_Context,
            chargeDirection,
            distance,
            speed,
            radius,
            damageMultiplier
        );
    } //public override IEnumerator Execute()

    //Inspector에서 Charge 설정값의 유효 범위 보정
    private void OnValidate()
    {
        m_Distance = Mathf.Max(0f, m_Distance);
        m_Speed = Mathf.Max(MinimumChargeSpeed, m_Speed);
        m_HitRadius = Mathf.Max(0f, m_HitRadius);
        m_WarningTime = Mathf.Max(0f, m_WarningTime);
        m_DamageMultiplier = Mathf.Max(0f, m_DamageMultiplier);
    } //private void OnValidate()

    //Charge 방향과 범위를 Warning Indicator로 표시
    private IEnumerator ShowWarning(
        MonsterAttackContext a_Context,
        Vector2 a_Origin,
        Vector2 a_Direction,
        float a_Distance,
        float a_Radius,
        float a_WarningTime)
    {
        MonsterAttackIndicator indicator = a_Context.Indicator;

        if (indicator != null)
        {
            indicator.ShowCharge(
                a_Origin,
                a_Direction,
                a_Distance,
                a_Radius * 2f
            );

            indicator.SetChargeProgress(0f);
        }

        float elapsedTime = 0f;

        while (elapsedTime < a_WarningTime)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                a_WarningTime > 0f
                    ? Mathf.Clamp01(elapsedTime / a_WarningTime)
                    : 1f;

            if (indicator != null)
                indicator.SetChargeProgress(progress);

            yield return null;
        }

        if (indicator != null)
        {
            indicator.SetChargeProgress(1f);
            indicator.Hide();
        }
    } //private IEnumerator ShowWarning()

    //고정된 방향으로 이동하며 장애물과 Damage Target 판정 처리
    private IEnumerator ExecuteCharge(
        MonsterAttackContext a_Context,
        Vector2 a_Direction,
        float a_TotalDistance,
        float a_Speed,
        float a_Radius,
        float a_DamageMultiplier)
    {
        Rigidbody2D rigidbody = a_Context.Rigidbody;

        if (rigidbody == null ||
            a_TotalDistance <= 0f ||
            a_Speed <= 0f)
        {
            yield break;
        }

        float traveledDistance = 0f;

        HashSet<CombatTarget> damagedTargets =
            new HashSet<CombatTarget>();

        WaitForFixedUpdate waitForFixedUpdate =
            new WaitForFixedUpdate();

        while (traveledDistance < a_TotalDistance)
        {
            /*
            Rigidbody2D.MovePosition과 Physics2D Cast를
            FixedUpdate 주기에 맞춰 처리합니다.
            */
            yield return waitForFixedUpdate;

            if (rigidbody == null)
                yield break;

            float remainingDistance =
                a_TotalDistance - traveledDistance;

            float stepDistance = Mathf.Min(
                a_Speed * Time.fixedDeltaTime,
                remainingDistance
            );

            if (stepDistance <= 0f)
                yield break;

            Vector2 currentPosition = rigidbody.position;

            if (TryGetObstacleDistance(
                    currentPosition,
                    a_Direction,
                    stepDistance,
                    a_Radius,
                    out float obstacleDistance))
            {
                float safeDistance = Mathf.Max(
                    0f,
                    obstacleDistance - ObstacleSkinWidth
                );

                if (safeDistance > 0f)
                {
                    DamagePath(
                        a_Context,
                        currentPosition,
                        a_Direction,
                        safeDistance,
                        a_Radius,
                        a_DamageMultiplier,
                        damagedTargets
                    );

                    Vector2 safePosition =
                        currentPosition +
                        a_Direction * safeDistance;

                    rigidbody.MovePosition(safePosition);
                }

                yield break;
            }

            DamagePath(
                a_Context,
                currentPosition,
                a_Direction,
                stepDistance,
                a_Radius,
                a_DamageMultiplier,
                damagedTargets
            );

            Vector2 nextPosition =
                currentPosition +
                a_Direction * stepDistance;

            rigidbody.MovePosition(nextPosition);

            traveledDistance += stepDistance;
        }
    } //private IEnumerator ExecuteCharge()

    //현재 이동 구간에 Charge를 차단하는 장애물이 있는지 확인
    private bool TryGetObstacleDistance(
        Vector2 a_StartPosition,
        Vector2 a_Direction,
        float a_Distance,
        float a_Radius,
        out float a_ObstacleDistance)
    {
        a_ObstacleDistance = 0f;

        if (m_ObstacleLayerMask.value == 0)
            return false;

        RaycastHit2D obstacleHit = Physics2D.CircleCast(
            a_StartPosition,
            a_Radius,
            a_Direction,
            a_Distance,
            m_ObstacleLayerMask
        );

        if (obstacleHit.collider == null)
            return false;

        a_ObstacleDistance = obstacleHit.distance;

        return true;
    } //private bool TryGetObstacleDistance()

    //Charge가 지나가는 하나의 이동 구간에서 유효 Target 피해 처리
    private void DamagePath(
        MonsterAttackContext a_Context,
        Vector2 a_StartPosition,
        Vector2 a_Direction,
        float a_Distance,
        float a_Radius,
        float a_DamageMultiplier,
        HashSet<CombatTarget> a_DamagedTargets)
    {
        if (a_Context == null ||
            a_DamagedTargets == null ||
            a_Distance <= 0f)
        {
            return;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            a_StartPosition,
            a_Radius,
            a_Direction,
            a_Distance
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;

            if (collider == null)
                continue;

            CombatTarget target =
                CombatTargetRegistry.GetTarget(collider);

            if (target == null)
                continue;

            if (a_Context.CanDamageTarget(target) == false)
                continue;

            /*
            하나의 Charge 전체에서 같은 Target은
            한 번만 피해를 받을 수 있습니다.
            */
            if (a_DamagedTargets.Add(target) == false)
                continue;

            Vector2 hitPoint = hits[i].point;

            if (hitPoint == Vector2.zero &&
                target.RootTransform != null)
            {
                hitPoint = target.RootTransform.position;
            }

            a_Context.ApplyDamage(
                target,
                hitPoint,
                a_Direction,
                a_DamageMultiplier,
                DamageSourceName
            );
        }
    } //private void DamagePath()
} //public class ChargeAttackStep