using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAttackController : MonoBehaviour
{
    /*
    Monster의 Attack Pattern을 조건, 우선순위와 Cooldown에 따라 선택하고
    Sequence의 Step 실행 및 공격 중 이동 정책과 Runtime 상태를 관리
    */

    [Header("Attack Patterns")]
    [SerializeField] private MonsterAttackPattern[] m_AttackPatterns;

    [Header("Runtime References")]
    [SerializeField] private MonsterAttackIndicator m_AttackIndicator;
    [SerializeField] private Transform m_ProjectileSpawnPoint;

    private MonsterController m_Controller;

    private float[] m_PatternCoolTimers;

    private Coroutine m_AttackCoroutine;

    private MonsterAttackSequence m_ActiveSequence;

    private int m_ActivePatternIndex = -1;

    private bool m_IsAttacking;
    private bool m_IsStepMovementLocked;
    private bool m_IsStoppingAttack;

    public bool IsAttacking => m_IsAttacking;

    public bool AllowMovementWhileAttacking
    {
        get
        {
            if (m_IsStepMovementLocked || m_IsAttacking == false || m_ActiveSequence == null)
                return false;

            return m_ActiveSequence.AllowMovementWhileAttacking;
        }
    }

    //Component 비활성화 시 진행 중인 공격과 Indicator 상태 정리
    private void OnDisable()
    {
        StopAttack();
    } //private void OnDisable()

    //Monster Controller를 연결하고 Attack Runtime 상태 초기화
    public void Initialize(MonsterController a_Controller)
    {
        StopAttack();

        m_Controller = a_Controller;
        m_IsAttacking = false;
        m_ActivePatternIndex = -1;
        m_ActiveSequence = null;
        m_IsStepMovementLocked = false;

        int patternCount = m_AttackPatterns != null ? m_AttackPatterns.Length : 0;
        m_PatternCoolTimers = new float[patternCount];

        if (m_AttackIndicator != null)
            m_AttackIndicator.Hide();
    } //public void Initialize()

    //각 Attack Pattern의 Cooldown Timer 갱신
    public void OnMonsterUpdate(float a_DeltaTime)
    {
        UpdatePatternCoolTimes(a_DeltaTime);
    } //public void OnMonsterUpdate()

    //현재 Target에 사용할 수 있는 최우선 Attack Pattern 실행 시도
    public bool TryStartAttack(CombatTarget a_Target)
    {
        if (m_IsAttacking)
            return false;

        int patternIndex = FindBestAttackPattern(
            a_Target,
            true,
            true
        );

        if (patternIndex < 0)
            return false;

        return StartPattern(
            patternIndex,
            a_Target
        );
    } //public bool TryStartAttack()

    //현재 Attack Step의 강제 이동 잠금 상태 설정
    public void SetStepMovementLocked(bool a_IsLocked)
    {
        m_IsStepMovementLocked = a_IsLocked;
    } //public void SetStepMovementLocked()

    /*
    AI가 현재 Target에게 어느 거리까지 접근해야 하는지 반환

    Cooldown이 끝난 공격 중 조건에 맞는 가장 높은 우선순위 Pattern을 우선 사용하고,
    모두 Cooldown 중이라면 조건에 맞는 Pattern 전체에서 접근 거리를 결정합니다.
    */
    public float GetApproachStopRange(CombatTarget a_Target)
    {
        int patternIndex = FindBestAttackPattern(
            a_Target,
            false,
            true
        );

        if (patternIndex < 0)
        {
            patternIndex = FindBestAttackPattern(
                a_Target,
                false,
                false
            );
        }

        if (patternIndex < 0)
            return 0f;

        MonsterAttackPattern pattern = m_AttackPatterns[patternIndex];

        if (pattern == null || pattern.Sequence == null)
            return 0f;

        return Mathf.Max(0f, pattern.Sequence.AttackAttemptRange);
    } //public float GetApproachStopRange()

    //Projectile을 생성할 기준 Transform 반환
    public Transform GetProjectileSpawnPoint()
    {
        return m_ProjectileSpawnPoint;
    } //public Transform GetProjectileSpawnPoint()

    //현재 실행 중인 공격을 즉시 중단하고 Runtime 상태 정리
    public void StopAttack()
    {
        if (m_AttackCoroutine != null)
        {
            m_IsStoppingAttack = true;

            StopCoroutine(m_AttackCoroutine);

            m_IsStoppingAttack = false;
            m_AttackCoroutine = null;
        }

        CompleteAttack(false);
    } //public void StopAttack()

    //Pattern별 남은 Cooldown 감소
    private void UpdatePatternCoolTimes(float a_DeltaTime)
    {
        if (m_PatternCoolTimers == null)
            return;

        float deltaTime = Mathf.Max(0f, a_DeltaTime);

        for (int i = 0; i < m_PatternCoolTimers.Length; i++)
        {
            if (m_PatternCoolTimers[i] <= 0f)
                continue;

            m_PatternCoolTimers[i] = Mathf.Max(
                0f,
                m_PatternCoolTimers[i] - deltaTime
            );
        }
    } //private void UpdatePatternCoolTimes()

    //선택된 Pattern의 Sequence 실행 시작
    private bool StartPattern(
        int a_PatternIndex,
        CombatTarget a_Target)
    {
        if (m_AttackPatterns == null || a_PatternIndex < 0 || a_PatternIndex >= m_AttackPatterns.Length)
            return false; 

        MonsterAttackPattern pattern = m_AttackPatterns[a_PatternIndex];

        if (pattern == null || pattern.Sequence == null)
            return false;

        m_ActivePatternIndex = a_PatternIndex;
        m_ActiveSequence = pattern.Sequence;
        m_IsStepMovementLocked = false;
        m_IsAttacking = true;

        Coroutine coroutine = StartCoroutine(
            AttackRoutine(
                m_ActiveSequence,
                a_Target
            )
        );

        /*
        Step이 하나도 없는 Sequence는 StartCoroutine 호출 중 즉시 완료될 수 있으므로
        완료 후에는 끝난 Coroutine Reference를 다시 저장하지 않습니다.
        */
        m_AttackCoroutine =
            m_IsAttacking
                ? coroutine
                : null;

        return true;
    } //private bool StartPattern()

    //Sequence에 등록된 Attack Step을 순서대로 실행
    private IEnumerator AttackRoutine(
        MonsterAttackSequence a_Sequence,
        CombatTarget a_Target)
    {
        MonsterAttackContext context = new MonsterAttackContext(
            m_Controller,
            this,
            a_Sequence,
            a_Target,
            m_AttackIndicator
        );

        try
        {
            MonsterAttackStep[] steps = a_Sequence.Steps;

            if (steps == null)
                yield break;

            for (int i = 0; i < steps.Length; i++)
            {
                MonsterAttackStep step = steps[i];

                if (step == null)
                    continue;

                yield return step.Execute(context);
            }
        }
        finally
        {
            /*
            StopAttack에서 명시적으로 중단하는 경우에는
            StopAttack 쪽에서 Cooldown 없이 상태를 정리합니다.
            */
            if (m_IsStoppingAttack == false)
                CompleteAttack(true);
        }
    } //private IEnumerator AttackRoutine()

    //공격 정상 완료 또는 중단 상태를 공통 규칙으로 정리
    private void CompleteAttack(bool a_ApplyCooldown)
    {
        if (a_ApplyCooldown)
            ApplyActivePatternCooldown();

        if (m_AttackIndicator != null)
            m_AttackIndicator.Hide();

        m_IsAttacking = false;
        m_AttackCoroutine = null;
        m_ActivePatternIndex = -1;
        m_ActiveSequence = null;
        m_IsStepMovementLocked = false;
    } //private void CompleteAttack()

    //현재 실행 중인 Pattern에 Sequence Cooldown 적용
    private void ApplyActivePatternCooldown()
    {
        if (m_ActivePatternIndex < 0 ||
            m_PatternCoolTimers == null ||
            m_ActivePatternIndex >= m_PatternCoolTimers.Length)
        {
            return;
        }

        float coolTime =
            m_ActiveSequence != null
                ? Mathf.Max(0f, m_ActiveSequence.AttackCoolTime)
                : 0f;

        m_PatternCoolTimers[m_ActivePatternIndex] = coolTime;
    } //private void ApplyActivePatternCooldown()

    //현재 Target에 대해 조건을 만족하는 가장 높은 Priority Pattern Index 탐색
    private int FindBestAttackPattern(
        CombatTarget a_Target,
        bool a_RequireAttackRange,
        bool a_RequireCoolTimeReady)
    {
        if (m_Controller == null || m_AttackPatterns == null || a_Target == null)
            return -1;

        if (a_Target.RootTransform == null)
            return -1;

        MonsterAttackConditionContext conditionContext =
            new MonsterAttackConditionContext(
                m_Controller,
                this,
                a_Target
            );

        float distance = conditionContext.DistanceToTarget;

        int bestIndex = -1;
        int bestPriority = int.MinValue;

        for (int i = 0; i < m_AttackPatterns.Length; i++)
        {
            MonsterAttackPattern pattern = m_AttackPatterns[i];

            if (pattern == null || pattern.Sequence == null)
                continue;

            if (a_RequireCoolTimeReady && IsPatternCoolingDown(i))
                continue;

            if (pattern.AreConditionsMet(conditionContext) == false)
                continue;

            MonsterAttackSequence sequence = pattern.Sequence;

            if (a_RequireAttackRange &&
                distance > Mathf.Max(0f, sequence.AttackAttemptRange))
            {
                continue;
            }

            if (pattern.Priority <= bestPriority)
                continue;

            bestPriority = pattern.Priority;
            bestIndex = i;
        }

        return bestIndex;
    } //private int FindBestAttackPattern()

    //지정 Pattern이 현재 Cooldown 중인지 반환
    private bool IsPatternCoolingDown(int a_PatternIndex)
    {
        if (m_PatternCoolTimers == null ||
            a_PatternIndex < 0 ||
            a_PatternIndex >= m_PatternCoolTimers.Length)
        {
            return false;
        }

        return m_PatternCoolTimers[a_PatternIndex] > 0f;
    } //private bool IsPatternCoolingDown()

    //선택된 Monster의 각 Attack Attempt Range를 Scene Gizmo로 표시
    private void OnDrawGizmosSelected()
    {
        if (m_AttackPatterns == null)
            return;

        for (int i = 0; i < m_AttackPatterns.Length; i++)
        {
            MonsterAttackPattern pattern = m_AttackPatterns[i];

            if (pattern == null || pattern.Sequence == null)
                continue;

            Gizmos.DrawWireSphere(
                transform.position,
                Mathf.Max(0f, pattern.Sequence.AttackAttemptRange)
            );
        }
    } //private void OnDrawGizmosSelected()
} //public class MonsterAttackController