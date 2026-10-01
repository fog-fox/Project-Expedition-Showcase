using UnityEngine;

[CreateAssetMenu(
    fileName = "HpRateAttackCondition",
    menuName = "Monster/Attack/Condition/HP Rate"
)]
public class HpRateAttackCondition : MonsterAttackCondition
{
    /*
    Monster의 현재 HP 비율이 지정된 최소/최대 구간 안에 있는지 검사하여
    해당 Attack을 사용할 수 있는 HP 조건을 판정
    */

    [Header("HP Rate")]
    [SerializeField, Range(0f, 1f)] private float m_MinHpRate;
    [SerializeField, Range(0f, 1f)] private float m_MaxHpRate = 1f;

    //Inspector에서 HP 비율 범위의 유효값 보정
    private void OnValidate()
    {
        m_MinHpRate = Mathf.Clamp01(m_MinHpRate);
        m_MaxHpRate = Mathf.Clamp01(m_MaxHpRate);

        if (m_MaxHpRate < m_MinHpRate)
            m_MaxHpRate = m_MinHpRate;
    } //private void OnValidate()

    //현재 Monster HP 비율이 지정 범위 안에 있는지 반환
    public override bool IsMet(MonsterAttackConditionContext a_Context)
    {
        if (a_Context == null)
            return false;

        float hpRate = Mathf.Clamp01(
            a_Context.GetHpRate()
        );

        float minHpRate = Mathf.Clamp01(m_MinHpRate);
        float maxHpRate = Mathf.Max(
            minHpRate,
            Mathf.Clamp01(m_MaxHpRate)
        );

        return hpRate >= minHpRate && hpRate <= maxHpRate;
    } //public override bool IsMet()
} //public class HpRateAttackCondition