using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerAuthorityController : NetworkBehaviour
{
    /*
    Network Player의 Input Authority와 State Authority를 확인하고
    기존 Player 시스템의 로컬 입력, 카메라 및 게임 상태 Simulation 활성 범위를 설정
    */

    [SerializeField] private Player m_Player;
    [SerializeField] private Rigidbody2D m_Rigidbody;

    public new bool HasInputAuthority => Object != null && Object.HasInputAuthority;
    public new bool HasStateAuthority => Object != null && Object.HasStateAuthority;

    //NetworkObject Spawn 완료 후 현재 Peer의 Authority에 맞게 Player Runtime 상태 설정
    public override void Spawned()
    {
        ResolveReferences();

        bool hasInputAuthority = Object.HasInputAuthority;
        bool hasStateAuthority = Object.HasStateAuthority;

        if (m_Player != null)
            m_Player.ConfigureNetworkAuthority(hasInputAuthority, hasStateAuthority);

        /*
        현재 단계에서는 Host만 Rigidbody2D Physics를 Simulation합니다.
        Client의 Player는 NetworkTransform으로 Host 상태를 표시합니다.
        */
        if (m_Rigidbody != null)
            m_Rigidbody.simulated = hasStateAuthority;

        Debug.Log(
            $"[NetworkPlayerAuthorityController] Player Spawn 완료. " +
            $"InputAuthority={hasInputAuthority}, StateAuthority={hasStateAuthority}",
            this
        );
    } //public override void Spawned()

    //필요한 Player Component Reference 확보
    private void ResolveReferences()
    {
        if (m_Player == null)
            m_Player = GetComponent<Player>();

        if (m_Rigidbody == null)
            m_Rigidbody = GetComponent<Rigidbody2D>();
    } //private void ResolveReferences()
}