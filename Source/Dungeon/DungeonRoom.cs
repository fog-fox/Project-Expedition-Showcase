using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum DungeonRoomType
{
    //플레이어가 Dungeon에 처음 진입하는 안전 방
    Start = 0,

    //일반 Monster 전투가 발생하는 방
    Normal = 1,

    //Dungeon의 최종 Boss 전투가 발생하는 방
    Boss = 2,

    //전투 없이 보상 상자가 배치되는 외곽 방
    Chest = 3
}

public enum DungeonRoomState
{
    //플레이어 진입을 기다리는 상태
    Waiting = 0,

    //Monster와 전투가 진행 중인 상태
    Battle = 1,

    //전투가 종료되어 통행 가능한 상태
    Cleared = 2
}

[DisallowMultipleComponent]
public class DungeonRoom : MonoBehaviour
{
    /*
    Dungeon Room의 Waiting, Battle, Cleared 상태를 관리하고
    Player 진입에 따른 Monster Spawn, Door 잠금 및 Room Clear 처리를 담당
    */

    [Header("Room Setting")]
    [FormerlySerializedAs("roomType")]
    [SerializeField] private DungeonRoomType m_RoomType = DungeonRoomType.Normal;

    [Header("Doors")]
    [FormerlySerializedAs("doors")]
    [SerializeField]
    private List<DungeonRoomDoor> m_Doors =
        new List<DungeonRoomDoor>();

    [Header("Monster Setting")]
    [FormerlySerializedAs("monsterPrefabs")]
    [SerializeField]
    private List<GameObject> m_MonsterPrefabs =
        new List<GameObject>();

    [FormerlySerializedAs("monsterSpawnPoints")]
    [SerializeField]
    private List<Transform> m_MonsterSpawnPoints =
        new List<Transform>();

    [Header("Debug")]
    [FormerlySerializedAs("roomState")]
    [SerializeField] private DungeonRoomState m_RoomState = DungeonRoomState.Waiting;

    private readonly List<GameObject> m_SpawnedMonsters =
        new List<GameObject>();

    public event Action<DungeonRoom> RoomCleared;

    public DungeonRoomType RoomType => m_RoomType;
    public DungeonRoomState RoomState => m_RoomState;
    public bool IsCleared => m_RoomState == DungeonRoomState.Cleared;

    //Room의 초기 상태와 Door 상태 설정
    private void Awake()
    {
        InitializeRoom();
    } //private void Awake()

    //Battle 중 파괴된 Monster Reference를 정리하고 Clear 여부 확인
    private void Update()
    {
        if (m_RoomState != DungeonRoomState.Battle)
            return;

        RemoveDeadMonsterReferences();

        if (m_SpawnedMonsters.Count == 0)
            ClearRoom();
    } //private void Update()

    //Inspector Collection이 null이 되지 않도록 보정
    private void OnValidate()
    {
        if (m_Doors == null)
            m_Doors = new List<DungeonRoomDoor>();

        if (m_MonsterPrefabs == null)
            m_MonsterPrefabs = new List<GameObject>();

        if (m_MonsterSpawnPoints == null)
            m_MonsterSpawnPoints = new List<Transform>();
    } //private void OnValidate()

    //방 종류에 맞춰 초기 상태 설정
    private void InitializeRoom()
    {
        m_SpawnedMonsters.Clear();

        InitializeDoors();

        switch (m_RoomType)
        {
            case DungeonRoomType.Start:
            case DungeonRoomType.Chest:
                m_RoomState = DungeonRoomState.Cleared;
                SetBattleDoorsLocked(false);
                break;

            case DungeonRoomType.Normal:
            case DungeonRoomType.Boss:
                m_RoomState = DungeonRoomState.Waiting;
                SetBattleDoorsLocked(false);
                break;
        }
    } //private void InitializeRoom()

    //등록된 모든 Door의 기본 상태 초기화
    private void InitializeDoors()
    {
        if (m_Doors == null)
            return;

        for (int i = 0; i < m_Doors.Count; i++)
        {
            DungeonRoomDoor door = m_Doors[i];

            if (door != null)
                door.InitializeDoor();
        }
    } //private void InitializeDoors()

    //Player가 방에 진입했을 때 방 종류와 상태에 따라 전투 시작
    public void PlayerEntered(GameObject a_Player)
    {
        if (m_RoomState != DungeonRoomState.Waiting)
            return;

        switch (m_RoomType)
        {
            case DungeonRoomType.Start:
            case DungeonRoomType.Chest:
                return;

            case DungeonRoomType.Normal:
            case DungeonRoomType.Boss:
                StartBattle();
                break;
        }
    } //public void PlayerEntered()

    //Room을 Battle 상태로 전환하고 Door 잠금 및 Monster Spawn 실행
    private void StartBattle()
    {
        if (m_RoomState != DungeonRoomState.Waiting)
            return;

        m_RoomState = DungeonRoomState.Battle;

        SetBattleDoorsLocked(true);
        SpawnMonsters();

        RemoveDeadMonsterReferences();

        if (m_SpawnedMonsters.Count == 0)
            ClearRoom();
    } //private void StartBattle()

    //등록된 Spawn Point마다 사용할 수 있는 Monster Prefab을 순환하여 생성
    private void SpawnMonsters()
    {
        m_SpawnedMonsters.Clear();

        if (m_MonsterPrefabs == null || m_MonsterPrefabs.Count == 0)
        {
            Debug.LogWarning(
                $"[{name}] 등록된 Monster Prefab이 없습니다.",
                this
            );

            return;
        }

        if (m_MonsterSpawnPoints == null || m_MonsterSpawnPoints.Count == 0)
        {
            Debug.LogWarning(
                $"[{name}] 등록된 Monster Spawn Point가 없습니다.",
                this
            );

            return;
        }

        List<GameObject> validPrefabs = GetValidMonsterPrefabs();

        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning(
                $"[{name}] 사용할 수 있는 Monster Prefab이 없습니다.",
                this
            );

            return;
        }

        for (int i = 0; i < m_MonsterSpawnPoints.Count; i++)
        {
            Transform spawnPoint = m_MonsterSpawnPoints[i];

            if (spawnPoint == null)
                continue;

            GameObject monsterPrefab =
                validPrefabs[i % validPrefabs.Count];

            GameObject monster = Instantiate(
                monsterPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            if (monster != null)
                m_SpawnedMonsters.Add(monster);
        }
    } //private void SpawnMonsters()

    //Monster Prefab 목록에서 null이 아닌 Prefab만 반환
    private List<GameObject> GetValidMonsterPrefabs()
    {
        List<GameObject> validPrefabs =
            new List<GameObject>(m_MonsterPrefabs.Count);

        for (int i = 0; i < m_MonsterPrefabs.Count; i++)
        {
            GameObject prefab = m_MonsterPrefabs[i];

            if (prefab != null)
                validPrefabs.Add(prefab);
        }

        return validPrefabs;
    } //private List<GameObject> GetValidMonsterPrefabs()

    //Destroy되어 null 상태가 된 Monster Reference 제거
    private void RemoveDeadMonsterReferences()
    {
        for (int i = m_SpawnedMonsters.Count - 1; i >= 0; i--)
        {
            if (m_SpawnedMonsters[i] == null)
                m_SpawnedMonsters.RemoveAt(i);
        }
    } //private void RemoveDeadMonsterReferences()

    //외부 Monster Death 통지를 받아 Spawn 목록과 Clear 상태 갱신
    public void NotifyMonsterDead(GameObject a_Monster)
    {
        if (m_RoomState != DungeonRoomState.Battle)
            return;

        if (a_Monster != null)
            m_SpawnedMonsters.Remove(a_Monster);
        else
            RemoveDeadMonsterReferences();

        if (m_SpawnedMonsters.Count == 0)
            ClearRoom();
    } //public void NotifyMonsterDead()

    //Room을 Cleared 상태로 전환하고 Door 해제 및 Clear Event 전달
    private void ClearRoom()
    {
        if (m_RoomState == DungeonRoomState.Cleared)
            return;

        m_RoomState = DungeonRoomState.Cleared;

        m_SpawnedMonsters.Clear();

        SetBattleDoorsLocked(false);

        OnRoomCleared();

        RoomCleared?.Invoke(this);
    } //private void ClearRoom()

    //Room Type별 Clear 이후 개별 처리를 위한 내부 확장 지점
    private void OnRoomCleared()
    {
        switch (m_RoomType)
        {
            case DungeonRoomType.Start:
                break;

            case DungeonRoomType.Normal:
                break;

            case DungeonRoomType.Boss:
                Debug.Log(
                    $"[{name}] Boss Room 클리어",
                    this
                );
                break;
        }
    } //private void OnRoomCleared()

    //등록된 모든 Door에 Battle 잠금 상태 적용
    private void SetBattleDoorsLocked(bool a_IsLocked)
    {
        if (m_Doors == null)
            return;

        for (int i = 0; i < m_Doors.Count; i++)
        {
            DungeonRoomDoor door = m_Doors[i];

            if (door != null)
                door.SetBattleLocked(a_IsLocked);
        }
    } //private void SetBattleDoorsLocked()
} //public class DungeonRoom