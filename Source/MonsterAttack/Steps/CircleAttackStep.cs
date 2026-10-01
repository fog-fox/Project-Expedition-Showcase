using System.Collections;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CircleAttackStep",
    menuName = "Monster/Attack/Step/Circle"
)]
public class CircleAttackStep : MonsterAttackStep
{
    /*
    공격 시작 위치를 중심으로 원형 Warning을 표시하고
    준비 시간이 끝나면 지정 범위에 한 번의 Circle Damage를 적용
    */

    private const string DamageSourceName = "Circle";

    [Header("Circle")]
    [SerializeField, Min(0f)] private float m_Range = 1.5f;
    [SerializeField, Min(0f)] private float m_WarningTime = 0.8f;
    [SerializeField, Min(0f)] private float m_DamageMultiplier = 1f;

    //Warning 표시 후 고정된 위치에 Circle Damage 적용
    public override IEnumerator Execute(MonsterAttackContext a_Context)
    {
        if (a_Context == null)
            yield break;

        Vector2 attackPosition = a_Context.GetAttackOrigin();

        float range = Mathf.Max(0f, m_Range);
        float warningTime = Mathf.Max(0f, m_WarningTime);
        float damageMultiplier = Mathf.Max(0f, m_DamageMultiplier);

        MonsterAttackIndicator indicator = a_Context.Indicator;
        bool isIndicatorVisible = false;

        try
        {
            if (indicator != null)
            {
                indicator.ShowCircle(
                    attackPosition,
                    range
                );

                indicator.SetProgress(0f);
                isIndicatorVisible = true;
            }

            float elapsedTime = 0f;

            while (elapsedTime < warningTime)
            {
                elapsedTime += Time.deltaTime;

                float progress =
                    warningTime > 0f
                        ? Mathf.Clamp01(elapsedTime / warningTime)
                        : 1f;

                if (indicator != null)
                    indicator.SetProgress(progress);

                yield return null;
            }

            if (indicator != null)
                indicator.SetProgress(1f);

            a_Context.ApplyCircleDamage(
                attackPosition,
                range,
                damageMultiplier,
                DamageSourceName
            );
        }
        finally
        {
            if (isIndicatorVisible && indicator != null)
                indicator.Hide();
        }
    } //public override IEnumerator Execute()

    //Inspector에서 Circle Attack 설정값의 유효 범위 보정
    private void OnValidate()
    {
        m_Range = Mathf.Max(0f, m_Range);
        m_WarningTime = Mathf.Max(0f, m_WarningTime);
        m_DamageMultiplier = Mathf.Max(0f, m_DamageMultiplier);
    } //private void OnValidate()
} //public class CircleAttackStep