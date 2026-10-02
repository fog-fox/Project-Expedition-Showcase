using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerStateController : NetworkBehaviour
{
    /*
    Network Player의 Character Profile과 지속 Gameplay 상태를 통합 관리하고
    Host의 authoritative Player 상태를 Network State로 복제하여 Proxy Runtime에 반영
    */

    private const int MaximumCharacterNameLength = 24;

    [Header("Reference")]
    [SerializeField] private Player m_Player;
    [SerializeField] private PlayerCharacterController m_CharacterController;
    [SerializeField] private PlayerInjuryController m_InjuryController;

    [Header("Fallback")]
    [SerializeField] private CharicType m_FallbackCharicType = CharicType.Chemist;

    [Networked] public PlayerNetworkState State { get; private set; }
    [Networked] public NetworkString<_32> CharacterName { get; private set; }
    [Networked] public int StateRevision { get; private set; }

    private int m_AppliedStateRevision = -1;
    private bool m_WasDead;

    //Network Spawn 완료 후 Local Profile 요청 및 현재 Network State 적용
    public override void Spawned()
    {
        ResolveReferences();

        m_AppliedStateRevision = -1;
        m_WasDead = false;

        if (Object.HasInputAuthority)
            RequestLocalCharacterProfile();

        ApplyNetworkStateIfNeeded();
    }

    //Host의 authoritative Gameplay 상태를 Network State에 반영
    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
            return;

        CaptureAuthorityState(false);
    }

    //수신한 Network State를 Proxy Runtime 상태에 반영
    public override void Render()
    {
        ApplyNetworkStateIfNeeded();
    }

    //Local Player가 선택한 Character Profile을 Host에 요청
    private void RequestLocalCharacterProfile()
    {
        CharicType characterType = GetLocalCharacterType();
        string characterName = GetLocalCharacterName();

        RPC_RequestCharacterProfile(characterType, characterName);
    }

    //Input Authority가 선택한 Character Profile을 Host에서 검증 및 확정
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestCharacterProfile(CharicType a_CharacterType, string a_CharacterName)
    {
        if (Object.HasStateAuthority == false)
            return;

        if (PlayerCharacterController.IsSupportedCharicType(a_CharacterType) == false)
        {
            Debug.LogWarning(
                $"[NetworkPlayerStateController] 지원하지 않는 Character Type 요청. " +
                $"Player={Object.InputAuthority}, Type={a_CharacterType}",
                this
            );

            return;
        }

        ResolveReferences();

        if (m_Player == null)
            return;

        string characterName = SanitizeCharacterName(a_CharacterName);

        CharacterName = characterName;

        m_Player.ApplyNetworkCharacterProfile(a_CharacterType, characterName);

        CaptureAuthorityState(true);

        Debug.Log(
            $"[NetworkPlayerStateController] Character Profile 확정. " +
            $"Player={Object.InputAuthority}, Type={a_CharacterType}, Name={characterName}",
            this
        );
    }

    //Host의 현재 Player Runtime 상태를 하나의 Network Snapshot으로 생성
    private void CaptureAuthorityState(bool a_ForceUpdate)
    {
        ResolveReferences();

        if (m_Player == null)
            return;

        Charic charicData = m_Player.GetCharicData();

        if (charicData == null)
            return;

        int maximumHealth = Mathf.Max(1, charicData.m_MaxHp);
        int currentHealth = Mathf.Clamp(charicData.m_CurHp, 0, maximumHealth);

        int injuryStack = m_InjuryController != null
            ? Mathf.Max(0, m_InjuryController.CurrentInjuryStack)
            : 0;

        bool hasPrimaryResource = TryGetPrimaryResourceState(
            charicData,
            out float resourceCurrent,
            out float resourceMaximum
        );

        PlayerNetworkState nextState = new PlayerNetworkState
        {
            CharacterType = charicData.m_ChrType,
            CurrentHealth = currentHealth,
            MaximumHealth = maximumHealth,
            InjuryStack = injuryStack,

            PrimaryResourceCurrent = resourceCurrent,
            PrimaryResourceMaximum = resourceMaximum,

            HasPrimaryResource = hasPrimaryResource,
            IsProfileReady = true,
            IsDead = currentHealth <= 0
        };

        if (a_ForceUpdate == false && AreStatesEqual(State, nextState))
            return;

        State = nextState;
        StateRevision++;
    }

    //현재 Character가 표시할 첫 번째 Runtime Resource 상태 반환
    private bool TryGetPrimaryResourceState(
        Charic a_CharicData,
        out float a_CurrentValue,
        out float a_MaximumValue)
    {
        a_CurrentValue = 0f;
        a_MaximumValue = 0f;

        if (a_CharicData is ICharicResourceProvider resourceProvider == false)
            return false;

        if (resourceProvider.GetResourceCount() <= 0)
            return false;

        CharicResourceInfo resourceInfo = resourceProvider.GetResourceInfo(0);

        if (resourceInfo.m_IsValid == false)
            return false;

        a_MaximumValue = Mathf.Max(0f, resourceInfo.m_MaxValue);
        a_CurrentValue = Mathf.Clamp(resourceInfo.m_CurValue, 0f, a_MaximumValue);

        return true;
    }

    //새로운 authoritative Network State를 Client Proxy Runtime에 적용
    private void ApplyNetworkStateIfNeeded()
    {
        if (Object.HasStateAuthority)
            return;

        PlayerNetworkState state = State;

        if (state.IsProfileReady == false)
            return;

        if (m_AppliedStateRevision == StateRevision)
            return;

        ResolveReferences();

        if (m_Player == null)
            return;

        if (EnsureProxyCharacter(state.CharacterType) == false)
            return;

        m_Player.ApplyNetworkHealthState(
            state.CurrentHealth,
            state.MaximumHealth
        );

        if (m_InjuryController != null &&
            m_InjuryController.ApplyNetworkInjuryState(state.InjuryStack) == false)
        {
            return;
        }

        ApplyProxyPrimaryResource(state);

        bool isDead = state.IsDead;

        if (m_WasDead == false && isDead)
            m_Player.HandleDead();

        m_WasDead = isDead;
        m_AppliedStateRevision = StateRevision;
    }

    //Network State의 Primary Resource 값을 Proxy Character 표시 상태에 적용
    private void ApplyProxyPrimaryResource(PlayerNetworkState a_State)
    {
        if ((bool)a_State.HasPrimaryResource == false)
            return;

        Charic charicData = m_Player != null ? m_Player.GetCharicData() : null;

        if (charicData == null)
            return;

        if (charicData is Chemist chemist)
        {
            chemist.m_MaxGas = Mathf.Max(0f, a_State.PrimaryResourceMaximum);
            chemist.m_CurGas = Mathf.Clamp(a_State.PrimaryResourceCurrent, 0f, chemist.m_MaxGas);
            return;
        }

        if (charicData is StackResourceActionCharic stackCharic)
        {
            stackCharic.ApplyNetworkResourceDisplayState(
                Mathf.RoundToInt(a_State.PrimaryResourceCurrent),
                Mathf.RoundToInt(a_State.PrimaryResourceMaximum)
            );
        }
    }

    //Proxy가 현재 Network Profile에 대응하는 Character를 보유하도록 보장
    private bool EnsureProxyCharacter(CharicType a_CharacterType)
    {
        if (PlayerCharacterController.IsSupportedCharicType(a_CharacterType) == false)
            return false;

        Charic currentCharic = m_Player.GetCharicData();
        string characterName = SanitizeCharacterName(CharacterName.ToString());

        if (currentCharic != null &&
            currentCharic.m_ChrType == a_CharacterType &&
            currentCharic.m_Name == characterName)
        {
            return true;
        }

        m_Player.ApplyNetworkCharacterProfile(a_CharacterType, characterName);

        return m_Player.GetCharicData() != null;
    }

    //두 Player Network State가 동일한지 확인
    private bool AreStatesEqual(PlayerNetworkState a_Left, PlayerNetworkState a_Right)
    {
        return a_Left.CharacterType == a_Right.CharacterType &&
               a_Left.CurrentHealth == a_Right.CurrentHealth &&
               a_Left.MaximumHealth == a_Right.MaximumHealth &&
               a_Left.InjuryStack == a_Right.InjuryStack &&
               Mathf.Approximately(a_Left.PrimaryResourceCurrent, a_Right.PrimaryResourceCurrent) &&
               Mathf.Approximately(a_Left.PrimaryResourceMaximum, a_Right.PrimaryResourceMaximum) &&
               (bool)a_Left.HasPrimaryResource == (bool)a_Right.HasPrimaryResource &&
               (bool)a_Left.IsProfileReady == (bool)a_Right.IsProfileReady &&
               (bool)a_Left.IsDead == (bool)a_Right.IsDead;
    }

    //현재 Local Player가 선택한 Character Type 반환
    private CharicType GetLocalCharacterType()
    {
        GameFlowManager gameFlowManager = GameFlowManager.Instance;

        if (gameFlowManager != null &&
            gameFlowManager.TryGetSelectedCharicType(out CharicType selectedCharacterType) &&
            PlayerCharacterController.IsSupportedCharicType(selectedCharacterType))
        {
            return selectedCharacterType;
        }

        ResolveReferences();

        if (m_CharacterController != null &&
            PlayerCharacterController.IsSupportedCharicType(m_CharacterController.StartCharicType))
        {
            return m_CharacterController.StartCharicType;
        }

        if (PlayerCharacterController.IsSupportedCharicType(m_FallbackCharicType))
            return m_FallbackCharicType;

        return CharicType.Chemist;
    }

    //현재 Local Account 또는 Character Controller의 Player 이름 반환
    private string GetLocalCharacterName()
    {
        ResolveReferences();

        if (m_CharacterController == null)
            return "Player";

        return SanitizeCharacterName(m_CharacterController.CharacterName);
    }

    //Network Character 이름을 허용 범위로 정리
    private string SanitizeCharacterName(string a_CharacterName)
    {
        string characterName = string.IsNullOrWhiteSpace(a_CharacterName)
            ? "Player"
            : a_CharacterName.Trim();

        if (characterName.Length > MaximumCharacterNameLength)
            characterName = characterName.Substring(0, MaximumCharacterNameLength);

        return characterName;
    }

    //필요한 Player 관련 Reference 확보
    private void ResolveReferences()
    {
        if (m_Player == null)
            m_Player = GetComponent<Player>();

        if (m_CharacterController == null)
            m_CharacterController = GetComponent<PlayerCharacterController>();

        if (m_InjuryController == null && m_Player != null)
            m_InjuryController = PlayerInjuryController.GetOrCreate(m_Player);
    }

#if UNITY_EDITOR
    //개발 테스트용 Host Authority Player HP 감소
    [ContextMenu("Debug Damage 10")]
    private void DebugDamage10()
    {
        if (Application.isPlaying == false || Object == null || Object.HasStateAuthority == false)
            return;

        ResolveReferences();

        Charic charicData = m_Player != null ? m_Player.GetCharicData() : null;

        if (charicData == null)
            return;

        charicData.m_CurHp = Mathf.Max(0, charicData.m_CurHp - 10);

        if (charicData.m_CurHp <= 0)
            m_Player.HandleDead();
    }

    //개발 테스트용 Host Authority Player HP 회복
    [ContextMenu("Debug Heal 10")]
    private void DebugHeal10()
    {
        if (Application.isPlaying == false || Object == null || Object.HasStateAuthority == false)
            return;

        ResolveReferences();

        Charic charicData = m_Player != null ? m_Player.GetCharicData() : null;

        if (charicData == null)
            return;

        charicData.m_CurHp = Mathf.Min(charicData.m_MaxHp, charicData.m_CurHp + 10);
    }

    //개발 테스트용 Host Authority Player 부상 추가
    [ContextMenu("Debug Add Injury")]
    private void DebugAddInjury()
    {
        if (Application.isPlaying == false || Object == null || Object.HasStateAuthority == false)
            return;

        ResolveReferences();

        if (m_InjuryController != null)
            m_InjuryController.RegisterHealthDamage(1);
    }

    //개발 테스트용 Host Authority Player 부상 전체 제거
    [ContextMenu("Debug Clear Injury")]
    private void DebugClearInjury()
    {
        if (Application.isPlaying == false || Object == null || Object.HasStateAuthority == false)
            return;

        ResolveReferences();

        if (m_InjuryController != null)
            m_InjuryController.RemoveAllInjury();
    }
#endif
}