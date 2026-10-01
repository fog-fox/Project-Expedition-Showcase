//Action이 실제 효과 위치까지 전달되는 방식
public enum ActionDeliveryType
{
    //Action 실행 위치에서 즉시 Impact 처리
    Instant = 0,

    //Projectile을 생성하고 충돌 또는 수명 종료 시 처리
    Projectile = 1,

    //지정 방향으로 즉시 Raycast하여 최초 유효 대상에 Impact 처리
    Hitscan = 2
} //public enum ActionDeliveryType

//Projectile 충돌 시 처리 방식
public enum ActionProjectileHitResponse
{
    //충돌 후 Projectile 제거
    Destroy = 0,

    //충돌 위치에 Action Impact 생성
    Deploy = 1,

    //충돌 후 Projectile 이동 유지
    Pierce = 2,

    //해당 충돌에 별도 반응하지 않음
    Ignore = 3
} //public enum ActionProjectileHitResponse

//Action Effect의 기준 대상 또는 범위 위치
public enum ActionTargetMode
{
    //시전자 자신을 대상으로 사용
    Caster = 0,

    //시전자 위치를 중심으로 범위 대상 탐색
    CasterRadius = 1,

    //지정되거나 충돌한 하나의 대상을 사용
    HitObject = 2,

    //Impact 위치를 중심으로 범위 대상 탐색
    ImpactRadius = 3
} //public enum ActionTargetMode

//범위 Action에서 사용할 영역 형태
public enum ActionAreaShape
{
    //원형 범위
    Circle = 0,

    //시전 방향을 기준으로 한 부채꼴 범위
    Sector = 1
} //public enum ActionAreaShape

//Action 대상과 시전자 사이의 관계 조건
public enum ActionTargetFilter
{
    //관계에 상관없이 유효한 모든 대상
    Any = 0,

    //시전자 자신
    Self = 1,

    //시전자와 같은 Team의 대상
    Allies = 2,

    //시전자와 다른 적대 Team의 대상
    Enemies = 3
} //public enum ActionTargetFilter

//Action이 대상에게 적용할 실제 Effect 유형
public enum ActionEffectType
{
    //HP 회복
    Heal = 0,

    //Damage 적용
    Damage = 1,

    //2, 3은 기존 Buff / Debuff가 사용하던 직렬화 값이므로 재사용하지 않음

    //시전자를 지정 방향으로 이동
    DashCaster = 4,

    //대상을 시전자 방향으로 끌어당김
    PullTarget = 5,

    //시전자를 대상의 후방으로 이동
    MoveBehindTarget = 6,

    //7, 8은 기존 StatBuff / TargetStatBuff가 사용하던 직렬화 값이므로 재사용하지 않음

    //Status Definition 적용
    ApplyStatus = 9,

    //Shield 적용
    ApplyShield = 10,

    //다음 기본 공격 강화 상태 등록
    ArmNextBasicAttack = 11,

    //벽 형태 Deployable 생성
    DeployWall = 12,

    //조건형 Trigger 등록
    RegisterTrigger = 13,

    //시전자의 Action 상태를 다음 상태로 전환
    ApplyRequestedActionState = 14,

    //대상의 Status를 Tag 기준으로 처리
    ConsumeStatusByTag = 15,

    //회복 후 최대 HP를 초과한 회복량을 Shield로 전환
    HealWithOverhealShield = 16,

    //대상을 Effect 중심에서 바깥 방향으로 밀어냄
    PushTarget = 17,

    //현재 위치에 반응형 지뢰 설치
    DeployReactiveMine = 18,

    //대상의 부상 Stack 제거
    RemoveInjury = 19,

    //Monster 우선 Target으로 사용할 Decoy 생성
    DeployDecoy = 20
} //public enum ActionEffectType

//Action Effect의 적용 시간 방식
public enum ActionApplyMode
{
    //Action 실행 시 한 번 적용
    Instant = 0,

    //지속시간 동안 주기적으로 적용
    OverTime = 1
} //public enum ActionApplyMode

//전투 대상이 소속된 Team 종류
public enum CombatTeamType
{
    //중립 대상
    Neutral = 0,

    //플레이어 Team
    Player = 1,

    //적 Team
    Enemy = 2
} //public enum CombatTeamType

//같은 Action 실행 안에서 동일 대상에게 Effect를 적용하는 방식
public enum ActionEffectTargetApplicationMode
{
    //Impact가 발생할 때마다 Effect 적용
    EveryImpact = 0,

    //하나의 Action 실행에서 같은 대상에게 한 번만 Effect 적용
    OncePerActionTarget = 1
} //public enum ActionEffectTargetApplicationMode

//Push Effect가 사용할 이동 방향 기준
public enum ActionPushDirectionMode
{
    //Impact 중심에서 대상 바깥 방향으로 밀어냄
    ImpactCenter = 0,

    //시전자 위치에서 대상 바깥 방향으로 밀어냄
    CasterToTarget = 1,

    //Action 사용 방향으로 밀어냄
    UseDirection = 2
} //public enum ActionPushDirectionMode

//DashCaster Effect에서 사용할 이동 방향
public enum ActionDashDirectionMode
{
    //Action 사용 방향으로 이동
    UseDirection = 0,

    //Action 사용 방향의 반대로 이동
    ReverseUseDirection = 1
} //public enum ActionDashDirectionMode