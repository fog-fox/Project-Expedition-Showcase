using System;
using UnityEngine;

public enum SkillUpgradeValueTarget
{
    //수정 대상 없음
    None = 0,

    //Damage 수치
    Damage = 1,

    //Heal 수치
    Heal = 2,

    //Effect 또는 Skill 범위
    Radius = 3,

    //Effect 지속시간
    Duration = 4,

    //주기 Effect 간격
    TickInterval = 5,

    //Knockback 수치
    Knockback = 6,

    //Projectile 이동속도
    ProjectileSpeed = 10,

    //Projectile 생존시간
    ProjectileLifeTime = 11,

    //Projectile 생성 위치 거리
    ProjectileSpawnDistance = 12,

    //Projectile 생성 수
    ProjectileCount = 13,

    //Projectile 확산 각도
    ProjectileSpreadAngle = 14,

    //Sector 범위 각도
    SectorAngle = 20,

    //Skill 시전 가능 거리
    CastRange = 21,

    //Skill 사용 시 Resource 소비량
    ResourceCost = 30,

    //Channel Skill의 초당 Resource 소비량
    ResourceCostPerSecond = 31,

    //Skill Cooldown
    Cooldown = 32,

    //Skill Cast 시간
    CastTime = 33,

    //Channel Skill의 Action 반복 간격
    ChannelActionInterval = 34
} //public enum SkillUpgradeValueTarget

[Serializable]
public class SkillUpgradeModifier
{
    /*
    Skill Upgrade가 변경할 Runtime 값과 연산 방식을 정의하고
    필요할 경우 특정 ActionEffect ID로 적용 대상을 제한하는 설정 데이터
    */

    [Header("Target")]
    [SerializeField]
    private SkillUpgradeValueTarget m_Target =
        SkillUpgradeValueTarget.None;

    [Header("Operation")]
    [SerializeField]
    private SkillModifierOperation m_Operation =
        SkillModifierOperation.AddFlat;

    [SerializeField] private float m_Value;

    [Header("Effect Filter")]
    [Tooltip(
        "Damage, Heal, Duration 등 특정 ActionEffect만 강화하려면 EffectId를 입력.\n" +
        "비워두면 조건에 맞는 모든 Effect에 적용."
    )]
    [SerializeField] private string m_TargetEffectId = string.Empty;

    public SkillUpgradeValueTarget Target => m_Target;
    public SkillModifierOperation Operation => m_Operation;
    public float Value => m_Value;
    public string TargetEffectId => m_TargetEffectId;

    //지정 Runtime Effect가 현재 Modifier의 Effect ID 조건을 충족하는지 확인
    public bool MatchesEffect(
        RuntimeActionEffectData a_Effect)
    {
        if (a_Effect == null)
            return false;

        if (string.IsNullOrWhiteSpace(m_TargetEffectId))
            return true;

        return string.Equals(
            m_TargetEffectId,
            a_Effect.EffectId,
            StringComparison.Ordinal
        );
    } //public bool MatchesEffect()
} //public class SkillUpgradeModifier