using System.Collections;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WaitAttackStep",
    menuName = "Monster/Attack/Step/Wait"
)]
public class WaitAttackStep : MonsterAttackStep
{
    /*
    Monster Attack Sequence 실행 중
    다음 Attack Step으로 넘어가기 전 지정된 시간만큼 대기
    */

    [SerializeField, Min(0f)] private float m_Duration = 0.5f;

    //Inspector에서 Wait Duration의 유효 범위 보정
    private void OnValidate()
    {
        m_Duration = Mathf.Max(0f, m_Duration);
    } //private void OnValidate()

    //설정된 Duration 동안 현재 Attack Sequence 실행 대기
    public override IEnumerator Execute(MonsterAttackContext a_Context)
    {
        float duration = Mathf.Max(0f, m_Duration);

        if (duration <= 0f)
            yield break;

        yield return new WaitForSeconds(duration);
    } //public override IEnumerator Execute()
} //public class WaitAttackStep