using Fusion;
using UnityEngine;

/*
Network Player가 Host로 전달할 Gameplay 버튼 종류
*/
public enum NetworkPlayerButton
{
    //기본 공격
    Attack = 0,

    //보조 기술
    Auxiliary = 1,

    //첫 번째 일반 기술
    Skill1 = 2,

    //두 번째 일반 기술
    Skill2 = 3,

    //세 번째 일반 기술
    Skill3 = 4,

    //궁극기
    Ultimate = 5
}

/*
Fusion Network Input으로 전달할 플레이어의
이동, 조준 위치 및 Gameplay 버튼 상태
*/
public struct NetworkPlayerInputData : INetworkInput
{
    public Vector2 MoveDirection;
    public Vector2 AimWorldPosition;
    public NetworkButtons Buttons;
}