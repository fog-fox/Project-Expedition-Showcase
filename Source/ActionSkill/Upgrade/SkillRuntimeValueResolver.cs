using UnityEngine;

public static class SkillRuntimeValueResolver
{
    /*
    SkillDefinition의 Upgrade 활성 상태를 확인하고
    Upgrade가 적용된 최종 Runtime 값을 계산하는 보조 유틸리티
    */

    //현재 Player에게 지정 Skill의 Upgrade가 활성화되어 있는지 확인
    public static bool IsUpgradeActive(
        SkillDefinition a_Definition,
        Player a_Player)
    {
        if (a_Definition == null ||
            a_Player == null ||
            a_Definition.UpgradeDefinition == null)
        {
            return false;
        }

        return a_Player.IsSkillUpgraded(a_Definition);
    } //public static bool IsUpgradeActive()

    //Player의 Upgrade 상태를 기준으로 최종 Skill 값 계산
    public static float ResolveValue(
        SkillDefinition a_Definition,
        Player a_Player,
        SkillUpgradeValueTarget a_Target,
        float a_BaseValue)
    {
        if (IsUpgradeActive(
                a_Definition,
                a_Player) == false)
        {
            return a_BaseValue;
        }

        return a_Definition.UpgradeDefinition.ResolveValue(
            a_Target,
            a_BaseValue
        );
    } //public static float ResolveValue()

    //Caster Object에서 Player를 찾아 Upgrade가 적용된 최종 Skill 값 계산
    public static float ResolveValue(
        SkillDefinition a_Definition,
        GameObject a_CasterObject,
        SkillUpgradeValueTarget a_Target,
        float a_BaseValue)
    {
        Player player = FindPlayer(a_CasterObject);

        return ResolveValue(
            a_Definition,
            player,
            a_Target,
            a_BaseValue
        );
    } //public static float ResolveValue()

    //Caster Object 또는 상위 Object에서 Player 탐색
    private static Player FindPlayer(
        GameObject a_CasterObject)
    {
        if (a_CasterObject == null)
            return null;

        Player player =
            a_CasterObject.GetComponent<Player>();

        if (player != null)
            return player;

        return a_CasterObject.GetComponentInParent<Player>();
    } //private static Player FindPlayer()
} //public static class SkillRuntimeValueResolver