using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    /*
    Photon Session의 Player 참가와 이탈을 감지하여
    Host에서 Network Player를 Spawn/Despawn하고 PlayerRef와 Player Object를 연결
    */

    private const float SpawnSpacing = 2f;

    private NetworkObject m_PlayerPrefab;
    private readonly Dictionary<PlayerRef, NetworkObject> m_SpawnedPlayers = new();

    //Network Player Spawn에 사용할 Prefab 설정
    public void Initialize(NetworkObject a_PlayerPrefab)
    {
        m_PlayerPrefab = a_PlayerPrefab;
    } //public void Initialize()

    //Player 참가 시 Host에서 해당 Player의 Network Player 생성
    public void PlayerJoined(PlayerRef a_Player)
    {
        if (Runner == null || Runner.IsServer == false)
            return;

        if (m_PlayerPrefab == null)
        {
            Debug.LogError("[NetworkPlayerSpawner] Network Player Prefab이 설정되지 않았습니다.", this);
            return;
        }

        if (Runner.TryGetPlayerObject(a_Player, out _))
            return;

        Vector3 spawnPosition = GetSpawnPosition(a_Player);

        NetworkObject playerObject = Runner.Spawn(
            m_PlayerPrefab,
            spawnPosition,
            Quaternion.identity,
            a_Player
        );

        if (playerObject == null)
        {
            Debug.LogError($"[NetworkPlayerSpawner] Player Spawn에 실패했습니다. Player={a_Player}", this);
            return;
        }

        Runner.SetPlayerObject(a_Player, playerObject);
        m_SpawnedPlayers[a_Player] = playerObject;

        Debug.Log(
            $"[NetworkPlayerSpawner] Player Spawn 완료. Player={a_Player}, Position={spawnPosition}",
            this
        );
    } //public void PlayerJoined()

    //Player 이탈 시 Host에서 해당 Network Player 제거
    public void PlayerLeft(PlayerRef a_Player)
    {
        if (Runner == null || Runner.IsServer == false)
            return;

        NetworkObject playerObject = null;

        if (m_SpawnedPlayers.TryGetValue(a_Player, out NetworkObject spawnedPlayer))
            playerObject = spawnedPlayer;
        else
            Runner.TryGetPlayerObject(a_Player, out playerObject);

        if (playerObject != null)
            Runner.Despawn(playerObject);

        m_SpawnedPlayers.Remove(a_Player);

        Debug.Log($"[NetworkPlayerSpawner] Player 제거 완료. Player={a_Player}", this);
    } //public void PlayerLeft()

    //개발 단계에서 Player마다 겹치지 않는 기본 Spawn 위치 계산
    private Vector3 GetSpawnPosition(PlayerRef a_Player)
    {
        int playerIndex = Mathf.Max(0, a_Player.RawEncoded - 1);
        return new Vector3(playerIndex * SpawnSpacing, 0f, 0f);
    } //private Vector3 GetSpawnPosition()
}