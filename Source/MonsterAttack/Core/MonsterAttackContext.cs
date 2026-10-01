using System.Collections.Generic;
using UnityEngine;

public class MonsterAttackContext
{
    /*
    Monster Attack Sequence 실행 중 필요한 Controller, Target, Indicator 정보를 제공하고
    Attack Step에서 공통으로 사용할 Target 판정, 위치 계산 및 Damage 처리를 담당
    */

    private const string DefaultAttackId = "Monster.Attack";
    private const string DefaultAttackName = "몬스터 공격";
    private const string DefaultCircleStepId = "Circle";

    private readonly HashSet<CombatTarget> m_DamagedTargets =
        new HashSet<CombatTarget>();

    public MonsterController Controller { get; }
    public MonsterAttackController AttackController { get; }
    public MonsterAttackSequence Sequence { get; }
    public CombatTarget Target { get; }
    public MonsterAttackIndicator Indicator { get; }

    public GameObject Attacker => Controller != null ? Controller.gameObject : null;
    public Rigidbody2D Rigidbody => Controller != null ? Controller.GetRigidbody() : null;

    //Monster Attack 실행에 필요한 Runtime Context 초기화
    public MonsterAttackContext(
        MonsterController a_Controller,
        MonsterAttackController a_AttackController,
        MonsterAttackSequence a_Sequence,
        CombatTarget a_Target,
        MonsterAttackIndicator a_Indicator)
    {
        Controller = a_Controller;
        AttackController = a_AttackController;
        Sequence = a_Sequence;
        Target = a_Target;
        Indicator = a_Indicator;
    } //public MonsterAttackContext()

    //현재 공격 Target이 유효한 Root Transform을 가지고 있는지 반환
    public bool HasValidTarget()
    {
        return Target != null && Target.RootTransform != null;
    } //public bool HasValidTarget()

    //현재 Monster의 공격 기준 위치 반환
    public Vector2 GetAttackOrigin()
    {
        Rigidbody2D rigidbody = Rigidbody;

        if (rigidbody != null)
            return rigidbody.position;

        GameObject attacker = Attacker;

        if (attacker != null)
            return attacker.transform.position;

        return Vector2.zero;
    } //public Vector2 GetAttackOrigin()

    //Projectile Spawn Point가 있으면 해당 위치를 사용하고 없으면 기본 공격 위치 반환
    public Vector2 GetProjectileOrigin()
    {
        if (AttackController != null)
        {
            Transform spawnPoint = AttackController.GetProjectileSpawnPoint();

            if (spawnPoint != null)
                return spawnPoint.position;
        }

        return GetAttackOrigin();
    } //public Vector2 GetProjectileOrigin()

    //지정 위치에서 현재 Target을 향하는 방향 Vector 반환
    public Vector2 GetDirectionToTarget(Vector2 a_Origin)
    {
        if (HasValidTarget() == false)
            return Vector2.zero;

        return (Vector2)Target.RootTransform.position - a_Origin;
    } //public Vector2 GetDirectionToTarget()

    //지정 CombatTarget이 현재 Monster가 공격할 수 있는 대상인지 반환
    public bool CanDamageTarget(CombatTarget a_Target)
    {
        if (a_Target == null)
            return false;

        GameObject attacker = Attacker;

        if (attacker != null && a_Target.RootObject == attacker)
            return false;

        CombatTeam team = a_Target.CombatTeam;

        if (team == null || team.TeamType != CombatTeamType.Player)
            return false;

        return a_Target.Damageable != null;
    } //public bool CanDamageTarget()

    //현재 Sequence 설정과 Damage Multiplier를 기반으로 Target에 피해 적용
    public void ApplyDamage(
        CombatTarget a_Target,
        Vector2 a_HitPoint,
        Vector2 a_HitDirection,
        float a_DamageMultiplier,
        string a_StepId = "")
    {
        if (CanDamageTarget(a_Target) == false || Controller == null)
            return;

        IDamageable damageable = a_Target.Damageable;

        if (damageable == null)
            return;

        float damage =
            Controller.GetAttack() *
            Mathf.Max(0f, a_DamageMultiplier);

        string attackId =
            Sequence != null
                ? Sequence.AttackId
                : DefaultAttackId;

        string attackName =
            Sequence != null
                ? Sequence.AttackName
                : DefaultAttackName;

        DamageType damageType =
            Sequence != null
                ? Sequence.DamageType
                : DamageType.Physical;

        if (string.IsNullOrEmpty(a_StepId) == false)
            attackId += "." + a_StepId;

        GameObject attacker = Attacker;

        DamageInfo damageInfo = new DamageInfo(
            attacker,
            attacker,
            attackId,
            attackName,
            damage,
            damageType,
            a_HitPoint,
            a_HitDirection
        );

        damageable.TakeDamage(damageInfo);
    } //public void ApplyDamage()

    //현재 Attack Step의 강제 이동 잠금 상태 설정
    public void SetMovementLocked(bool a_IsLocked)
    {
        if (AttackController == null)
            return;

        AttackController.SetStepMovementLocked(a_IsLocked);
    } //public void SetMovementLocked()

    //지정 위치를 중심으로 Circle 범위 안의 모든 유효 Target에 한 번씩 피해 적용
    public void ApplyCircleDamage(
        Vector2 a_Position,
        float a_Range,
        float a_DamageMultiplier,
        string a_StepId = DefaultCircleStepId)
    {
        float range = Mathf.Max(0f, a_Range);

        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            a_Position,
            range
        );

        m_DamagedTargets.Clear();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if (collider == null)
                continue;

            CombatTarget target = CombatTargetRegistry.GetTarget(collider);

            if (target == null)
                continue;

            /*
            하나의 Target이 여러 Collider를 가지고 있어도
            한 번의 Circle Damage에서는 한 번만 판정합니다.
            */
            if (m_DamagedTargets.Add(target) == false)
                continue;

            if (CanDamageTarget(target) == false)
                continue;

            Transform targetTransform = target.RootTransform;

            if (targetTransform == null)
                continue;

            Vector2 targetPosition = targetTransform.position;
            Vector2 hitDirection = targetPosition - a_Position;

            ApplyDamage(
                target,
                targetPosition,
                hitDirection,
                a_DamageMultiplier,
                a_StepId
            );
        }
    } //public void ApplyCircleDamage()
} //public class MonsterAttackContext