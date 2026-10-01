using System.Collections;
using UnityEngine;

public abstract class MonsterAttackStep : ScriptableObject
{
    /*
    Monster Attack Sequence를 구성하는
    모든 Attack Step ScriptableObject의 공통 기반 클래스
    */

    //현재 Attack Context를 사용하여 Step 실행
    public abstract IEnumerator Execute(MonsterAttackContext a_Context);
} //public abstract class MonsterAttackStep