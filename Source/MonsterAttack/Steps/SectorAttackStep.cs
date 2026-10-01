using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SectorAttackStep",
    menuName = "Monster/Attack/Step/Sector"
)]
public class SectorAttackStep : MonsterAttackStep
{
    /*
    부채꼴 공격 범위와 Timing을 사전에 표시하고
    Warning 종료 시 Range와 Angle 안의 유효 Target에게 피해를 적용
    */

    private const float MinimumDirectionSqrMagnitude = 0.001f;
    private const float FullCircleAngle = 360f;
    private const string StepId = "Sector";

    [Header("Sector")]
    [SerializeField, Min(0f)] private float m_Range = 3f;
    [SerializeField, Range(1f, 360f)] private float m_Angle = 90f;
    [SerializeField, Min(0f)] private float m_WarningTime = 0.8f;
    [SerializeField, Min(0f)] private float m_DamageMultiplier = 1f;

    [Header("Aim")]
    [SerializeField] private bool m_TrackTargetDuringWarning;

    //Inspector에서 Sector Attack 설정값의 유효 범위 보정
    private void OnValidate()
    {
        m_Range = Mathf.Max(0f, m_Range);
        m_Angle = Mathf.Clamp(m_Angle, 1f, FullCircleAngle);
        m_WarningTime = Mathf.Max(0f, m_WarningTime);
        m_DamageMultiplier = Mathf.Max(0f, m_DamageMultiplier);
    } //private void OnValidate()

    //Sector Warning을 표시한 뒤 최종 위치와 방향을 기준으로 피해 적용
    public override IEnumerator Execute(MonsterAttackContext a_Context)
    {
        if (a_Context == null)
            yield break;

        float range = Mathf.Max(0f, m_Range);
        float angle = Mathf.Clamp(m_Angle, 1f, FullCircleAngle);
        float warningTime = Mathf.Max(0f, m_WarningTime);
        float damageMultiplier = Mathf.Max(0f, m_DamageMultiplier);

        Vector2 origin = a_Context.GetAttackOrigin();
        Vector2 direction = a_Context.GetDirectionToTarget(origin);

        if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            yield break;

        direction.Normalize();

        MonsterAttackIndicator indicator = a_Context.Indicator;

        try
        {
            if (indicator != null)
            {
                indicator.ShowSector(
                    origin,
                    direction,
                    range,
                    angle
                );

                indicator.SetSectorProgress(0f);
            }

            float elapsedTime = 0f;

            while (elapsedTime < warningTime)
            {
                elapsedTime += Time.deltaTime;

                if (m_TrackTargetDuringWarning)
                {
                    origin = a_Context.GetAttackOrigin();

                    Vector2 currentDirection = a_Context.GetDirectionToTarget(origin);

                    if (currentDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
                        direction = currentDirection.normalized;

                    if (indicator != null)
                    {
                        indicator.UpdateSector(
                            origin,
                            direction,
                            range,
                            angle
                        );
                    }
                }

                if (indicator != null)
                    indicator.SetSectorProgress(Mathf.Clamp01(elapsedTime / warningTime));

                yield return null;
            }

            ExecuteDamage(
                a_Context,
                origin,
                direction,
                range,
                angle,
                damageMultiplier
            );
        }
        finally
        {
            if (indicator != null)
                indicator.Hide();
        }
    } //public override IEnumerator Execute()

    //Sector Range와 Angle 안의 모든 유효 Target에게 한 번씩 피해 적용
    private void ExecuteDamage(
        MonsterAttackContext a_Context,
        Vector2 a_Origin,
        Vector2 a_Direction,
        float a_Range,
        float a_Angle,
        float a_DamageMultiplier)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            a_Origin,
            a_Range
        );

        HashSet<CombatTarget> damagedTargets = new HashSet<CombatTarget>();

        bool fullCircle = a_Angle >= FullCircleAngle;
        float halfAngle = a_Angle * 0.5f;
        float minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if (collider == null)
                continue;

            CombatTarget target = CombatTargetRegistry.GetTarget(collider);

            if (target == null || damagedTargets.Add(target) == false)
                continue;

            if (a_Context.CanDamageTarget(target) == false)
                continue;

            Transform targetTransform = target.RootTransform;

            if (targetTransform == null)
                continue;

            Vector2 targetPosition = targetTransform.position;
            Vector2 targetDirection = targetPosition - a_Origin;

            Vector2 hitDirection;

            /*
            Target 중심이 공격 Origin과 정확히 겹친 경우에도
            Sector 내부에 있는 것으로 판단하여 피해를 적용합니다.
            */
            if (targetDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                hitDirection = a_Direction;
            }
            else
            {
                hitDirection = targetDirection.normalized;

                if (fullCircle == false &&
                    Vector2.Dot(a_Direction, hitDirection) < minimumDot)
                {
                    continue;
                }
            }

            a_Context.ApplyDamage(
                target,
                targetPosition,
                hitDirection,
                a_DamageMultiplier,
                StepId
            );
        }
    } //private void ExecuteDamage()
} //public class SectorAttackStep