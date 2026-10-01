using System;
using UnityEngine;

[Serializable]
public class MonsterAttackPattern
{
    /*
    Monster의 하나의 Attack Pattern에 사용할 Sequence, Priority와
    모든 실행 Condition을 직렬화하여 관리
    */

    [Header("Pattern")]
    [SerializeField] private string m_Name = "Attack Pattern";
    [SerializeField] private MonsterAttackSequence m_Sequence;
    [SerializeField] private int m_Priority;

    [Header("Conditions")]
    [SerializeField]
    private MonsterAttackCondition[] m_Conditions = Array.Empty<MonsterAttackCondition>();

    public string Name => m_Name;
    public MonsterAttackSequence Sequence => m_Sequence;
    public int Priority => m_Priority;

    //등록된 모든 Attack Condition이 현재 Context를 만족하는지 반환
    public bool AreConditionsMet(MonsterAttackConditionContext a_Context)
    {
        if (m_Conditions == null || m_Conditions.Length == 0)
            return true;

        for (int i = 0; i < m_Conditions.Length; i++)
        {
            MonsterAttackCondition condition = m_Conditions[i];

            if (condition == null)
                continue;

            if (condition.IsMet(a_Context) == false)
                return false;
        }

        return true;
    } //public bool AreConditionsMet()
} //public class MonsterAttackPattern