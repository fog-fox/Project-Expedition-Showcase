using Fusion;

/*
Network Player의 지속적인 Gameplay 상태를 하나의 Snapshot으로 묶어
State Authority에서 Proxy로 동기화하기 위한 Network State
*/
public struct PlayerNetworkState : INetworkStruct
{
    public CharicType CharacterType;

    public int CurrentHealth;
    public int MaximumHealth;
    public int InjuryStack;

    public float PrimaryResourceCurrent;
    public float PrimaryResourceMaximum;

    public NetworkBool HasPrimaryResource;
    public NetworkBool IsProfileReady;
    public NetworkBool IsDead;
}