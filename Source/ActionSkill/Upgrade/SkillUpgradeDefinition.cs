using UnityEngine;

[CreateAssetMenu(
    menuName = "Last Expedition/Skill/Skill Upgrade Definition",
    fileName = "New Skill Upgrade Definition"
)]
public class SkillUpgradeDefinition : ScriptableObject
{
    /*
    Skill Upgrade에 적용할 Modifier 목록을 정의하고
    Runtime Skill 데이터 또는 개별 Skill 수치에 강화 효과를 적용하는 데이터 Asset
    */

    [Header("Description")]
    [TextArea(3, 8)]
    [SerializeField] private string m_UpgradeDescription = string.Empty;

    [Header("Modifiers")]
    [SerializeField] private SkillUpgradeModifier[] m_Modifiers;

    public string UpgradeDescription => m_UpgradeDescription;
    public int ModifierCount => m_Modifiers != null ? m_Modifiers.Length : 0;

    //지정 Index에 등록된 Skill Upgrade Modifier 반환
    public SkillUpgradeModifier GetModifier(int a_Index)
    {
        if (m_Modifiers == null ||
            a_Index < 0 ||
            a_Index >= m_Modifiers.Length)
        {
            return null;
        }

        return m_Modifiers[a_Index];
    } //public SkillUpgradeModifier GetModifier()

    //등록된 모든 Upgrade Modifier를 Runtime Skill 데이터에 연산 순서대로 적용
    public void ApplyTo(RuntimeSkillData a_RuntimeData)
    {
        if (a_RuntimeData == null)
            return;

        ApplyByOperation(
            a_RuntimeData,
            SkillModifierOperation.AddFlat
        );

        ApplyByOperation(
            a_RuntimeData,
            SkillModifierOperation.AddPercent
        );

        ApplyByOperation(
            a_RuntimeData,
            SkillModifierOperation.Multiply
        );
    } //public void ApplyTo()

    //지정 Skill 수치에 Upgrade Modifier를 연산 순서대로 적용하여 최종 값 반환
    public float ResolveValue(
        SkillUpgradeValueTarget a_Target,
        float a_BaseValue)
    {
        float value = a_BaseValue;

        value = ApplyValueByOperation(
            a_Target,
            value,
            SkillModifierOperation.AddFlat
        );

        value = ApplyValueByOperation(
            a_Target,
            value,
            SkillModifierOperation.AddPercent
        );

        value = ApplyValueByOperation(
            a_Target,
            value,
            SkillModifierOperation.Multiply
        );

        return value;
    } //public float ResolveValue()

    //지정 연산 방식에 해당하는 Modifier를 Runtime Skill 데이터에 적용
    private void ApplyByOperation(
        RuntimeSkillData a_RuntimeData,
        SkillModifierOperation a_Operation)
    {
        if (m_Modifiers == null)
            return;

        for (int i = 0; i < m_Modifiers.Length; i++)
        {
            SkillUpgradeModifier modifier = m_Modifiers[i];

            if (modifier == null ||
                modifier.Operation != a_Operation)
            {
                continue;
            }

            a_RuntimeData.ApplyUpgradeModifier(modifier);
        }
    } //private void ApplyByOperation()

    //지정 대상과 연산 방식에 해당하는 Modifier를 현재 값에 적용
    private float ApplyValueByOperation(
        SkillUpgradeValueTarget a_Target,
        float a_CurrentValue,
        SkillModifierOperation a_Operation)
    {
        if (m_Modifiers == null)
            return a_CurrentValue;

        float value = a_CurrentValue;

        for (int i = 0; i < m_Modifiers.Length; i++)
        {
            SkillUpgradeModifier modifier = m_Modifiers[i];

            if (modifier == null ||
                modifier.Target != a_Target ||
                modifier.Operation != a_Operation)
            {
                continue;
            }

            value = ApplyValue(
                value,
                a_Operation,
                modifier.Value
            );
        }

        return value;
    } //private float ApplyValueByOperation()

    //현재 값에 지정 Skill Modifier 연산 적용
    private static float ApplyValue(
        float a_CurrentValue,
        SkillModifierOperation a_Operation,
        float a_Value)
    {
        switch (a_Operation)
        {
            case SkillModifierOperation.AddFlat:
                return a_CurrentValue + a_Value;

            case SkillModifierOperation.AddPercent:
                return a_CurrentValue * (1f + a_Value);

            case SkillModifierOperation.Multiply:
                return a_CurrentValue * a_Value;

            default:
                return a_CurrentValue;
        }
    } //private static float ApplyValue()
} //public class SkillUpgradeDefinition