using System.Collections;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ProjectileAttackStep",
    menuName = "Monster/Attack/Step/Projectile"
)]
public class ProjectileAttackStep : MonsterAttackStep
{
    /*
    Target을 발사 직전까지 추적하며 Projectile 방향을 예고하고
    Warning 종료 시 마지막으로 표시된 방향으로 하나 이상의 Projectile을 발사
    */

    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumProjectileLifeTime = 0.1f;
    private const float MinimumBlinkInterval = 0.02f;
    private const string DefaultAttackId = "Monster.Projectile";
    private const string DefaultAttackName = "몬스터 공격";
    private const string ProjectileStepId = "Projectile";

    [Header("Projectile")]
    [SerializeField] private MonsterProjectile m_ProjectilePrefab;
    [SerializeField, Min(0f)] private float m_ProjectileSpeed = 10f;
    [SerializeField, Min(MinimumProjectileLifeTime)] private float m_ProjectileLifeTime = 5f;
    [SerializeField, Min(0f)] private float m_DamageMultiplier = 1f;

    [Header("Projectile Spread")]
    [SerializeField, Min(1)] private int m_ProjectileCount = 1;
    [SerializeField, Range(0f, 360f)] private float m_SpreadAngle;

    [Header("Aim")]
    [SerializeField, Min(0f)] private float m_WarningTime = 1f;
    [SerializeField, Min(0f)] private float m_AimLineLength = 10f;
    [SerializeField, Min(0f)] private float m_AimLineWidth = 0.1f;
    [SerializeField, Min(0f)] private float m_BlinkDuration = 0.3f;
    [SerializeField, Min(MinimumBlinkInterval)] private float m_BlinkInterval = 0.08f;

    //Inspector에서 Projectile Attack 설정값의 유효 범위 보정
    private void OnValidate()
    {
        m_ProjectileSpeed = Mathf.Max(0f, m_ProjectileSpeed);
        m_ProjectileLifeTime = Mathf.Max(MinimumProjectileLifeTime, m_ProjectileLifeTime);
        m_DamageMultiplier = Mathf.Max(0f, m_DamageMultiplier);

        m_ProjectileCount = Mathf.Max(1, m_ProjectileCount);
        m_SpreadAngle = Mathf.Clamp(m_SpreadAngle, 0f, 360f);

        m_WarningTime = Mathf.Max(0f, m_WarningTime);
        m_AimLineLength = Mathf.Max(0f, m_AimLineLength);
        m_AimLineWidth = Mathf.Max(0f, m_AimLineWidth);
        m_BlinkDuration = Mathf.Max(0f, m_BlinkDuration);
        m_BlinkInterval = Mathf.Max(MinimumBlinkInterval, m_BlinkInterval);
    } //private void OnValidate()

    //Target을 추적하며 조준선을 표시한 뒤 Projectile 발사
    public override IEnumerator Execute(MonsterAttackContext a_Context)
    {
        if (a_Context == null || m_ProjectilePrefab == null)
            yield break;

        int projectileCount = Mathf.Max(1, m_ProjectileCount);
        float spreadAngle = Mathf.Clamp(m_SpreadAngle, 0f, 360f);
        float warningTime = Mathf.Max(0f, m_WarningTime);
        float blinkDuration = Mathf.Clamp(m_BlinkDuration, 0f, warningTime);
        float blinkStart = warningTime - blinkDuration;

        Vector2 origin = a_Context.GetProjectileOrigin();
        Vector2 aimDirection = a_Context.GetDirectionToTarget(origin);

        if (aimDirection.sqrMagnitude <= MinimumDirectionMagnitude)
            yield break;

        aimDirection.Normalize();

        /*
        Projectile 방향 배열은 공격 시작 시 한 번만 생성하고
        Warning 중에는 배열 내용을 갱신하여 재사용합니다.
        */
        Vector2[] projectileDirections = new Vector2[projectileCount];

        UpdateProjectileDirections(
            projectileDirections,
            aimDirection,
            spreadAngle
        );

        MonsterAttackIndicator indicator = a_Context.Indicator;

        try
        {
            if (indicator != null)
            {
                indicator.ShowLines(
                    origin,
                    projectileDirections,
                    Mathf.Max(0f, m_AimLineLength),
                    Mathf.Max(0f, m_AimLineWidth)
                );
            }

            float elapsedTime = 0f;

            while (elapsedTime < warningTime)
            {
                elapsedTime += Time.deltaTime;

                origin = a_Context.GetProjectileOrigin();

                /*
                중심 조준 방향은 Warning이 끝날 때까지 Target을 추적합니다.
                Target이 일시적으로 유효하지 않으면 마지막 유효 방향을 유지합니다.
                */
                Vector2 currentDirection = a_Context.GetDirectionToTarget(origin);

                if (currentDirection.sqrMagnitude > MinimumDirectionMagnitude)
                    aimDirection = currentDirection.normalized;

                UpdateProjectileDirections(
                    projectileDirections,
                    aimDirection,
                    spreadAngle
                );

                if (indicator != null)
                {
                    indicator.UpdateLines(
                        origin,
                        projectileDirections,
                        Mathf.Max(0f, m_AimLineLength)
                    );

                    UpdateBlink(
                        indicator,
                        elapsedTime,
                        blinkStart
                    );
                }

                yield return null;
            }

            /*
            마지막으로 Player에게 표시된 방향과 위치를 그대로 사용하여
            Warning Indicator와 실제 Projectile 방향이 일치하도록 유지합니다.
            */
            for (int i = 0; i < projectileDirections.Length; i++)
            {
                FireProjectile(
                    a_Context,
                    origin,
                    projectileDirections[i]
                );
            }
        }
        finally
        {
            if (indicator != null)
                indicator.Hide();
        }
    } //public override IEnumerator Execute()

    //중심 방향과 Spread 설정을 사용하여 모든 Projectile 방향 갱신
    private void UpdateProjectileDirections(
        Vector2[] a_Directions,
        Vector2 a_CenterDirection,
        float a_SpreadAngle)
    {
        if (a_Directions == null || a_Directions.Length == 0)
            return;

        for (int i = 0; i < a_Directions.Length; i++)
        {
            a_Directions[i] = AttackDirectionUtility.GetSpreadDirection(
                a_CenterDirection,
                a_SpreadAngle,
                i,
                a_Directions.Length
            );
        }
    } //private void UpdateProjectileDirections()

    //Warning 종료 직전 Aim Line의 점멸 상태 갱신
    private void UpdateBlink(
        MonsterAttackIndicator a_Indicator,
        float a_ElapsedTime,
        float a_BlinkStart)
    {
        if (a_Indicator == null)
            return;

        if (a_ElapsedTime < a_BlinkStart)
        {
            a_Indicator.SetLinesVisible(true);
            return;
        }

        float blinkElapsed = a_ElapsedTime - a_BlinkStart;
        float interval = Mathf.Max(MinimumBlinkInterval, m_BlinkInterval);
        int blinkIndex = Mathf.FloorToInt(blinkElapsed / interval);

        a_Indicator.SetLinesVisible(blinkIndex % 2 == 0);
    } //private void UpdateBlink()

    //지정 위치와 방향으로 Monster Projectile 생성 및 공격 정보 초기화
    private void FireProjectile(
        MonsterAttackContext a_Context,
        Vector2 a_Origin,
        Vector2 a_Direction)
    {
        MonsterProjectile projectile = Object.Instantiate(
            m_ProjectilePrefab,
            a_Origin,
            Quaternion.identity
        );

        if (projectile == null)
            return;

        float baseDamage =
            a_Context.Controller != null
                ? a_Context.Controller.GetAttack()
                : 0f;

        float damage = baseDamage * Mathf.Max(0f, m_DamageMultiplier);

        MonsterAttackSequence sequence = a_Context.Sequence;

        string attackId =
            sequence != null
                ? sequence.AttackId + "." + ProjectileStepId
                : DefaultAttackId;

        string attackName =
            sequence != null
                ? sequence.AttackName
                : DefaultAttackName;

        DamageType damageType =
            sequence != null
                ? sequence.DamageType
                : DamageType.Physical;

        projectile.Initialize(
            a_Context.Attacker,
            attackId,
            attackName,
            damage,
            damageType,
            a_Direction,
            Mathf.Max(0f, m_ProjectileSpeed),
            Mathf.Max(MinimumProjectileLifeTime, m_ProjectileLifeTime)
        );
    } //private void FireProjectile()
} //public class ProjectileAttackStep