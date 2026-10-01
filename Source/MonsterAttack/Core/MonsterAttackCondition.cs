using UnityEngine;

public abstract class MonsterAttackCondition : ScriptableObject
{
    /*
    Monster Attack의 실행 가능 여부를 판정하는
    모든 Attack Condition ScriptableObject의 공통 기반 클래스
    */

    //현재 Monster Attack Context가 조건을 만족하는지 반환
    public abstract bool IsMet(MonsterAttackConditionContext a_Context);
} //public abstract class MonsterAttackCondition