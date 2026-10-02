using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerInputProvider : NetworkBehaviour, INetworkRunnerCallbacks
{
    /*
    Input Authority를 가진 Local Network Player의 이동, 조준 및 버튼 입력을
    Update에서 누적하고 Fusion NetworkInput으로 Host에 전달
    */

    [SerializeField] private Player m_Player;
    [SerializeField] private PlayerInputHandler m_InputHandler;

    private NetworkPlayerInputData m_CurrentInput;

    private uint m_UnsentPressedMask;
    private uint m_PendingReleaseMask;
    private uint m_DeferredReleaseMask;

    //Network Spawn 완료 후 Input Authority인 경우 Runner Callback 등록
    public override void Spawned()
    {
        ResolveReferences();

        if (Object.HasInputAuthority)
            Runner.AddCallbacks(this);
    }

    //Network Despawn 시 Runner Callback 해제
    public override void Despawned(NetworkRunner a_Runner, bool a_HasState)
    {
        if (a_Runner != null)
            a_Runner.RemoveCallbacks(this);
    }

    //매 Frame Local Player 입력 누적
    private void Update()
    {
        if (Object == null || Object.HasInputAuthority == false)
            return;

        ResolveReferences();
        ApplyDeferredReleases();

        m_CurrentInput.MoveDirection = m_InputHandler != null
            ? m_InputHandler.CurrentMoveInput
            : Vector2.zero;

        if (m_Player != null)
            m_CurrentInput.AimWorldPosition = m_Player.GetSkillAimMouseWorldPosition();

        CaptureButton(
            NetworkPlayerButton.Attack,
            Input.GetMouseButtonDown(0),
            Input.GetMouseButton(0),
            Input.GetMouseButtonUp(0)
        );

        CaptureButton(
            NetworkPlayerButton.Auxiliary,
            Input.GetMouseButtonDown(1),
            Input.GetMouseButton(1),
            Input.GetMouseButtonUp(1)
        );

        if (m_InputHandler == null)
            return;

        CaptureKeyButton(NetworkPlayerButton.Skill1, m_InputHandler.Skill1Key);
        CaptureKeyButton(NetworkPlayerButton.Skill2, m_InputHandler.Skill2Key);
        CaptureKeyButton(NetworkPlayerButton.Skill3, m_InputHandler.Skill3Key);
        CaptureKeyButton(NetworkPlayerButton.Ultimate, m_InputHandler.UltimateKey);
    }

    //지정 KeyCode의 Press, Hold, Release 상태 누적
    private void CaptureKeyButton(NetworkPlayerButton a_Button, KeyCode a_KeyCode)
    {
        CaptureButton(
            a_Button,
            Input.GetKeyDown(a_KeyCode),
            Input.GetKey(a_KeyCode),
            Input.GetKeyUp(a_KeyCode)
        );
    }

    //하나의 버튼 입력을 Fusion Tick 사이에서도 유실되지 않도록 누적
    private void CaptureButton(
        NetworkPlayerButton a_Button,
        bool a_WasPressed,
        bool a_IsHeld,
        bool a_WasReleased)
    {
        uint mask = GetButtonMask(a_Button);

        if (a_WasPressed)
        {
            m_CurrentInput.Buttons.Set(a_Button, true);
            m_UnsentPressedMask |= mask;
        }

        if (a_WasReleased)
        {
            if ((m_UnsentPressedMask & mask) != 0)
            {
                m_PendingReleaseMask |= mask;
                return;
            }

            m_CurrentInput.Buttons.Set(a_Button, false);
            return;
        }

        if (a_WasPressed)
            return;

        if ((m_UnsentPressedMask & mask) != 0 || (m_PendingReleaseMask & mask) != 0)
            return;

        m_CurrentInput.Buttons.Set(a_Button, a_IsHeld);
    }

    //이전 OnInput에서 예약된 Release 상태 적용
    private void ApplyDeferredReleases()
    {
        if (m_DeferredReleaseMask == 0)
            return;

        for (int i = 0; i <= (int)NetworkPlayerButton.Ultimate; i++)
        {
            uint mask = 1u << i;

            if ((m_DeferredReleaseMask & mask) == 0)
                continue;

            m_CurrentInput.Buttons.Set((NetworkPlayerButton)i, false);
        }

        m_DeferredReleaseMask = 0;
    }

    //Button Enum을 내부 Bit Mask로 변환
    private uint GetButtonMask(NetworkPlayerButton a_Button)
    {
        return 1u << (int)a_Button;
    }

    //Fusion Tick에 현재 누적된 Local Input 전달
    public void OnInput(NetworkRunner a_Runner, NetworkInput a_Input)
    {
        a_Input.Set(m_CurrentInput);

        uint releaseAfterSendMask = m_PendingReleaseMask & m_UnsentPressedMask;

        m_UnsentPressedMask = 0;
        m_PendingReleaseMask &= ~releaseAfterSendMask;
        m_DeferredReleaseMask |= releaseAfterSendMask;
    }

    //필요한 Player Component Reference 확보
    private void ResolveReferences()
    {
        if (m_Player == null)
            m_Player = GetComponent<Player>();

        if (m_InputHandler == null)
            m_InputHandler = GetComponent<PlayerInputHandler>();
    }

    public void OnObjectExitAOI(NetworkRunner a_Runner, NetworkObject a_Object, PlayerRef a_Player) { }
    public void OnObjectEnterAOI(NetworkRunner a_Runner, NetworkObject a_Object, PlayerRef a_Player) { }
    public void OnPlayerJoined(NetworkRunner a_Runner, PlayerRef a_Player) { }
    public void OnPlayerLeft(NetworkRunner a_Runner, PlayerRef a_Player) { }
    public void OnInputMissing(NetworkRunner a_Runner, PlayerRef a_Player, NetworkInput a_Input) { }
    public void OnShutdown(NetworkRunner a_Runner, ShutdownReason a_ShutdownReason) { }
    public void OnConnectedToServer(NetworkRunner a_Runner) { }
    public void OnDisconnectedFromServer(NetworkRunner a_Runner, NetDisconnectReason a_Reason) { }
    public void OnConnectRequest(NetworkRunner a_Runner, NetworkRunnerCallbackArgs.ConnectRequest a_Request, byte[] a_Token) { }
    public void OnConnectFailed(NetworkRunner a_Runner, NetAddress a_RemoteAddress, NetConnectFailedReason a_Reason) { }
    public void OnSessionListUpdated(NetworkRunner a_Runner, List<SessionInfo> a_SessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner a_Runner, Dictionary<string, object> a_Data) { }
    public void OnHostMigration(NetworkRunner a_Runner, HostMigrationToken a_HostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner a_Runner, PlayerRef a_Player, ReliableKey a_Key, ReadOnlySpan<byte> a_Data) { }
    public void OnReliableDataProgress(NetworkRunner a_Runner, PlayerRef a_Player, ReliableKey a_Key, float a_Progress) { }
    public void OnSceneLoadDone(NetworkRunner a_Runner) { }
    public void OnSceneLoadStart(NetworkRunner a_Runner) { }
}