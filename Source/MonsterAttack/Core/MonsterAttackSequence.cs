using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MonsterAttackSequence",
    menuName = "Monster/Attack/Sequence"
)]
public class MonsterAttackSequence : ScriptableObject
{
    /*
    하나의 Monster Attack을 구성하는 식별 정보, Damage Type,
    실행 거리, Cooldown, 이동 정책 및 Attack Step Sequence를 정의
    */

    [Header("Attack")]
    [SerializeField] private string m_AttackId = "Monster.Attack";
    [SerializeField] private string m_AttackName = "몬스터 공격";
    [SerializeField] private DamageType m_DamageType = DamageType.Physical;

    [Header("Condition")]
    [SerializeField, Min(0f)] private float m_AttackAttemptRange = 1f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float m_AttackCoolTime = 1.5f;

    [Header("Movement")]
    [SerializeField] private bool m_AllowMovementWhileAttacking;

    [Header("Sequence")]
    [SerializeField]
    private MonsterAttackStep[] m_Steps =
        Array.Empty<MonsterAttackStep>();

    public string AttackId => m_AttackId;
    public string AttackName => m_AttackName;
    public DamageType DamageType => m_DamageType;
    public float AttackAttemptRange => Mathf.Max(0f, m_AttackAttemptRange);
    public float AttackCoolTime => Mathf.Max(0f, m_AttackCoolTime);
    public bool AllowMovementWhileAttacking => m_AllowMovementWhileAttacking;
    public MonsterAttackStep[] Steps => m_Steps;

    //Inspector에서 Attack Sequence 설정값의 유효 범위 보정
    private void OnValidate()
    {
        m_AttackAttemptRange = Mathf.Max(0f, m_AttackAttemptRange);
        m_AttackCoolTime = Mathf.Max(0f, m_AttackCoolTime);

        if (m_Steps == null)
            m_Steps = Array.Empty<MonsterAttackStep>();
    } //private void OnValidate()
} //public class MonsterAttackSequence