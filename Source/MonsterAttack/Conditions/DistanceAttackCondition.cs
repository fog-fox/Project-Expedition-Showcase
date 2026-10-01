using UnityEngine;

[CreateAssetMenu(
    fileName = "DistanceAttackCondition",
    menuName = "Monster/Attack/Condition/Distance"
)]
public class DistanceAttackCondition : MonsterAttackCondition
{
    /*
    Monster와 Target 사이의 거리가 지정된 최소/최대 범위 안에 있는지 검사하여
    해당 Attack을 사용할 수 있는 거리 조건을 판정
    */

    [Header("Distance")]
    [SerializeField, Min(0f)] private float m_MinDistance;
    [SerializeField, Min(0f)] private float m_MaxDistance = 999f;

    //Inspector에서 공격 거리 범위의 유효값 보정
    private void OnValidate()
    {
        m_MinDistance = Mathf.Max(0f, m_MinDistance);
        m_MaxDistance = Mathf.Max(m_MinDistance, m_MaxDistance);
    } //private void OnValidate()

    //현재 Target까지의 거리가 지정 범위 안에 있는지 반환
    public override bool IsMet(MonsterAttackConditionContext a_Context)
    {
        if (a_Context == null)
            return false;

        float distance = a_Context.DistanceToTarget;

        float minDistance = Mathf.Max(0f, m_MinDistance);
        float maxDistance = Mathf.Max(minDistance, m_MaxDistance);

        return
            distance >= minDistance &&
            distance <= maxDistance;
    } //public override bool IsMet()
} //public class DistanceAttackCondition