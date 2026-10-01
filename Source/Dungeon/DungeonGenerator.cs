using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonGenerator : MonoBehaviour
{
    /*
    Grid 기반 Dungeon Layout을 생성하고 Start, Normal, Boss Room을 배치하며
    Room Connection, Door 상태, Corridor 및 Player Spawn 정보를 관리
    */

    private const float MinimumRoomSize = 0.1f;

    public event Action DungeonGenerated;

    [Header("Generation")]
    [FormerlySerializedAs("normalRoomCount")]
    [SerializeField, Min(1)] private int m_NormalRoomCount = 10;

    [FormerlySerializedAs("useRandomSeed")]
    [SerializeField] private bool m_UseRandomSeed = true;

    [FormerlySerializedAs("seed")]
    [SerializeField] private int m_Seed = 12345;

    [Header("Room Placement")]
    [FormerlySerializedAs("roomSize")]
    [SerializeField] private Vector2 m_RoomSize = new Vector2(16f, 10f);

    [FormerlySerializedAs("corridorGap")]
    [SerializeField, Min(0f)] private float m_CorridorGap = 5f;

    [Header("Special Room Prefabs")]
    [FormerlySerializedAs("startRoomPrefab")]
    [SerializeField] private GameObject m_StartRoomPrefab;

    [Header("Chest Room")]
    [SerializeField, Min(0)] private int m_ChestRoomCount = 1;
    [SerializeField] private GameObject m_ChestRoomPrefab;

    private readonly List<Vector2Int> m_ChestRoomCells = new List<Vector2Int>();

    public IReadOnlyList<Vector2Int> ChestRoomCells => m_ChestRoomCells;

    [FormerlySerializedAs("bossRoomPrefab")]
    [SerializeField] private GameObject m_BossRoomPrefab;

    [Header("Normal Room Prefabs")]
    [FormerlySerializedAs("normalRoomPrefabs")]
    [SerializeField]
    private List<GameObject> m_NormalRoomPrefabs =
        new List<GameObject>();

    [Header("Hierarchy")]
    [FormerlySerializedAs("roomRoot")]
    [SerializeField] private Transform m_RoomRoot;

    [Header("Corridor")]
    [FormerlySerializedAs("corridorGenerator")]
    [SerializeField] private DungeonCorridorGenerator m_CorridorGenerator;

    [Header("Generation Option")]
    [FormerlySerializedAs("allowLoops")]
    [SerializeField] private bool m_AllowLoops;

    [Header("Debug")]
    [FormerlySerializedAs("generateOnStart")]
    [SerializeField] private bool m_GenerateOnStart = true;

    [FormerlySerializedAs("startCell")]
    [SerializeField] private Vector2Int m_StartCell = Vector2Int.zero;

    [FormerlySerializedAs("bossCell")]
    [SerializeField] private Vector2Int m_BossCell;

    private readonly List<Vector2Int> m_NormalRoomCells =
        new List<Vector2Int>();

    private readonly HashSet<Vector2Int> m_OccupiedCells =
        new HashSet<Vector2Int>();

    private readonly List<DungeonConnection> m_Connections =
        new List<DungeonConnection>();

    private readonly Dictionary<Vector2Int, GameObject> m_SpawnedRooms =
        new Dictionary<Vector2Int, GameObject>();

    private System.Random m_Random;

    private DungeonStartRoom m_SpawnedStartRoom;
    private DungeonBossRoom m_SpawnedBossRoom;

    public Vector2Int StartCell => m_StartCell;
    public Vector2Int BossCell => m_BossCell;
    public Vector2 RoomSize => m_RoomSize;
    public float CorridorGap => m_CorridorGap;

    public IReadOnlyList<DungeonConnection> Connections => m_Connections;

    public IReadOnlyDictionary<Vector2Int, GameObject> SpawnedRooms =>
        m_SpawnedRooms;

    public DungeonStartRoom SpawnedStartRoom => m_SpawnedStartRoom;
    public DungeonBossRoom SpawnedBossRoom => m_SpawnedBossRoom;

    public Vector3 StartWorldPosition => GetCellWorldPosition(m_StartCell);
    public Vector3 BossWorldPosition => GetCellWorldPosition(m_BossCell);

    public Vector3 PlayerSpawnPosition =>
        m_SpawnedStartRoom != null
            ? m_SpawnedStartRoom.PlayerSpawnPosition
            : StartWorldPosition;

    public Quaternion PlayerSpawnRotation =>
        m_SpawnedStartRoom != null
            ? m_SpawnedStartRoom.PlayerSpawnRotation
            : Quaternion.identity;

    //설정에 따라 Scene 시작 시 Dungeon 자동 생성
    private void Start()
    {
        if (m_GenerateOnStart)
            GenerateDungeon();
    } //private void Start()

    //Inspector에서 Dungeon 생성 설정값과 Reference 보정
    private void OnValidate()
    {
        m_NormalRoomCount = Mathf.Max(1, m_NormalRoomCount);

        m_RoomSize.x = Mathf.Max(MinimumRoomSize, m_RoomSize.x);
        m_RoomSize.y = Mathf.Max(MinimumRoomSize, m_RoomSize.y);

        m_CorridorGap = Mathf.Max(0f, m_CorridorGap);

        if (m_NormalRoomPrefabs == null)
            m_NormalRoomPrefabs = new List<GameObject>();

        ResolveReferences();
    } //private void OnValidate()

    //Dungeon 전체 구조 생성
    public void GenerateDungeon()
    {
        ClearDungeon();
        InitializeRandom();

        GenerateLayout();
        SelectBossCell();
        GenerateChestRoomCells();

        BuildConnections();

        SpawnStartRoom();
        SpawnNormalRooms();
        SpawnChestRooms();
        SpawnBossRoom();

        ApplyRoomConnections();

        if (m_CorridorGenerator != null)
            m_CorridorGenerator.GenerateCorridors();

        DungeonGenerated?.Invoke();
    }

    //현재 생성된 Dungeon Room과 Corridor 및 Runtime 상태 제거
    public void ClearDungeon()
    {
        if (m_CorridorGenerator != null)
            m_CorridorGenerator.ClearCorridors();

        m_NormalRoomCells.Clear();
        m_ChestRoomCells.Clear();
        m_OccupiedCells.Clear();
        m_Connections.Clear();

        foreach (KeyValuePair<Vector2Int, GameObject> pair in m_SpawnedRooms)
        {
            GameObject roomObject = pair.Value;

            if (roomObject == null)
                continue;

            /*
            Destroy는 Frame 종료까지 지연되므로 재생성 전에
            기존 Collider와 Behaviour가 즉시 동작하지 않도록 비활성화합니다.
            */
            roomObject.SetActive(false);
            Destroy(roomObject);
        }

        m_SpawnedRooms.Clear();

        m_SpawnedStartRoom = null;
        m_SpawnedBossRoom = null;
    } //public void ClearDungeon()

    /*
    ChestRoom을 배치할 빈 Cell과 해당 ChestRoom이 연결될 NormalRoom Cell을 보관
    */
    private readonly struct ChestRoomCandidate
    {
        public Vector2Int Cell { get; }
        public Vector2Int ParentCell { get; }

        //ChestRoom 후보 정보 생성
        public ChestRoomCandidate(Vector2Int a_Cell, Vector2Int a_ParentCell)
        {
            Cell = a_Cell;
            ParentCell = a_ParentCell;
        }
    }

    //지정 Cell에 Spawn된 Room이 있는지 확인
    public bool TryGetSpawnedRoom(
        Vector2Int a_Cell,
        out GameObject a_RoomObject)
    {
        return m_SpawnedRooms.TryGetValue(a_Cell, out a_RoomObject);
    } //public bool TryGetSpawnedRoom()

    //Grid Cell을 Dungeon World Position으로 변환
    public Vector3 GetCellWorldPosition(Vector2Int a_Cell)
    {
        float horizontalStep = m_RoomSize.x + m_CorridorGap;
        float verticalStep = m_RoomSize.y + m_CorridorGap;

        return GetDungeonOrigin() +
               new Vector3(
                   a_Cell.x * horizontalStep,
                   a_Cell.y * verticalStep,
                   0f
               );
    } //public Vector3 GetCellWorldPosition()

    //고정 Seed 또는 Runtime Random Seed를 사용하여 난수 Generator 초기화
    private void InitializeRandom()
    {
        if (m_UseRandomSeed)
            m_Seed = Environment.TickCount;

        m_Random = new System.Random(m_Seed);
    } //private void InitializeRandom()

    //Start Cell에서 확장하며 지정 수만큼 Normal Room Cell 생성
    private void GenerateLayout()
    {
        m_OccupiedCells.Add(m_StartCell);

        int targetRoomCount = Mathf.Max(1, m_NormalRoomCount);

        while (m_NormalRoomCells.Count < targetRoomCount)
        {
            List<Vector2Int> expandableCells = GetExpandableCells();

            if (expandableCells.Count == 0)
            {
                Debug.LogError(
                    $"[{name}] 더 이상 확장 가능한 Room Cell이 없습니다. " +
                    $"생성={m_NormalRoomCells.Count}/{targetRoomCount}",
                    this
                );

                break;
            }

            Vector2Int parentCell =
                expandableCells[m_Random.Next(expandableCells.Count)];

            List<DungeonDirection> directions = GetShuffledDirections();

            bool generated = false;

            for (int i = 0; i < directions.Count; i++)
            {
                Vector2Int targetCell =
                    parentCell +
                    DungeonDirectionUtility.ToOffset(directions[i]);

                if (m_OccupiedCells.Contains(targetCell))
                    continue;

                if (m_AllowLoops == false &&
                    CountOccupiedNeighbours(targetCell) > 1)
                {
                    continue;
                }

                m_OccupiedCells.Add(targetCell);
                m_NormalRoomCells.Add(targetCell);

                generated = true;
                break;
            }

            /*
            GetExpandableCells에서 이미 확장 가능한 Cell만 반환하므로
            일반적으로 도달하지 않지만 상태 변화에 대한 안전장치로 유지합니다.
            */
            if (generated == false)
                continue;
        }
    } //private void GenerateLayout()

    //현재 Layout에서 새로운 Room을 추가할 수 있는 모든 Cell 반환
    private List<Vector2Int> GetExpandableCells()
    {
        List<Vector2Int> expandable =
            new List<Vector2Int>(m_NormalRoomCells.Count + 1);

        /*
        HashSet 순회 순서에 의존하지 않고 고정된 생성 순서를 사용하여
        동일 Seed가 같은 Layout을 재현하기 쉽게 유지합니다.
        */
        if (HasAvailableNeighbour(m_StartCell))
            expandable.Add(m_StartCell);

        for (int i = 0; i < m_NormalRoomCells.Count; i++)
        {
            Vector2Int cell = m_NormalRoomCells[i];

            if (HasAvailableNeighbour(cell))
                expandable.Add(cell);
        }

        return expandable;
    } //private List<Vector2Int> GetExpandableCells()

    //지정 Cell 주변에 현재 생성 규칙으로 사용할 수 있는 빈 Cell이 있는지 반환
    private bool HasAvailableNeighbour(Vector2Int a_Cell)
    {
        foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
        {
            Vector2Int neighbour =
                a_Cell + DungeonDirectionUtility.ToOffset(direction);

            if (m_OccupiedCells.Contains(neighbour))
                continue;

            if (m_AllowLoops == false &&
                CountOccupiedNeighbours(neighbour) > 1)
            {
                continue;
            }

            return true;
        }

        return false;
    } //private bool HasAvailableNeighbour()

    //지정 Cell의 상하좌우 중 현재 점유된 Cell 수 반환
    private int CountOccupiedNeighbours(Vector2Int a_Cell)
    {
        int count = 0;

        foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
        {
            Vector2Int neighbour =
                a_Cell + DungeonDirectionUtility.ToOffset(direction);

            if (m_OccupiedCells.Contains(neighbour))
                count++;
        }

        return count;
    } //private int CountOccupiedNeighbours()

    //Dungeon Direction 목록을 현재 Seed Random으로 Shuffle하여 반환
    private List<DungeonDirection> GetShuffledDirections()
    {
        List<DungeonDirection> directions =
            new List<DungeonDirection>(DungeonDirectionUtility.AllDirections);

        for (int i = directions.Count - 1; i > 0; i--)
        {
            int targetIndex = m_Random.Next(i + 1);

            DungeonDirection temp = directions[i];
            directions[i] = directions[targetIndex];
            directions[targetIndex] = temp;
        }

        return directions;
    } //private List<DungeonDirection> GetShuffledDirections()

    //Start에서 가능한 한 먼 Room의 바깥 Cell을 Boss Room 위치로 선택
    private void SelectBossCell()
    {
        if (m_NormalRoomCells.Count == 0)
        {
            m_BossCell = m_StartCell + Vector2Int.right;
            m_OccupiedCells.Add(m_BossCell);
            return;
        }

        Dictionary<Vector2Int, int> distances = CalculateDistancesFromStart();

        List<Vector2Int> orderedRoomCells =
            new List<Vector2Int>(m_NormalRoomCells);

        /*
        Start로부터 먼 Room부터 Boss 후보를 검색합니다.
        동일 거리에서는 Cell 좌표를 기준으로 정렬하여 Seed 재현성을 유지합니다.
        */
        orderedRoomCells.Sort(
            (a_CellA, a_CellB) =>
            {
                int distanceA =
                    distances.TryGetValue(a_CellA, out int valueA)
                        ? valueA
                        : -1;

                int distanceB =
                    distances.TryGetValue(a_CellB, out int valueB)
                        ? valueB
                        : -1;

                int distanceCompare = distanceB.CompareTo(distanceA);

                if (distanceCompare != 0)
                    return distanceCompare;

                return CompareCells(a_CellA, a_CellB);
            }
        );

        bool requireSingleNeighbour = m_AllowLoops == false;

        if (TrySelectBossCell(
                orderedRoomCells,
                requireSingleNeighbour,
                out Vector2Int selectedBossCell))
        {
            m_BossCell = selectedBossCell;
            m_OccupiedCells.Add(m_BossCell);
            return;
        }

        /*
        Loop를 허용하지 않는 설정에서 모든 단일 연결 후보를 찾지 못한 경우에도
        Dungeon 생성 자체가 실패하지 않도록 마지막 수단으로 빈 인접 Cell을 사용합니다.
        이 경우 Boss Cell이 둘 이상의 기존 Room과 인접하여 Loop가 생길 수 있습니다.
        */
        if (requireSingleNeighbour &&
            TrySelectBossCell(
                orderedRoomCells,
                false,
                out selectedBossCell))
        {
            Debug.LogWarning(
                $"[{name}] 단일 연결 Boss Room 후보가 없어 " +
                "복수 연결이 가능한 후보를 사용합니다.",
                this
            );

            m_BossCell = selectedBossCell;
            m_OccupiedCells.Add(m_BossCell);
            return;
        }

        Debug.LogError(
            $"[{name}] Boss Room 위치를 결정할 수 없습니다.",
            this
        );

        m_BossCell = orderedRoomCells[0];
    } //private void SelectBossCell()

    //거리순 Room 목록에서 조건을 만족하는 Boss Cell 후보 선택
    private bool TrySelectBossCell(
        List<Vector2Int> a_OrderedRoomCells,
        bool a_RequireSingleNeighbour,
        out Vector2Int a_BossCell)
    {
        a_BossCell = default;

        if (a_OrderedRoomCells == null || a_OrderedRoomCells.Count == 0)
            return false;

        for (int i = 0; i < a_OrderedRoomCells.Count; i++)
        {
            Vector2Int parentCell = a_OrderedRoomCells[i];

            List<Vector2Int> candidates = GetBossCandidates(
                parentCell,
                a_RequireSingleNeighbour
            );

            if (candidates.Count == 0)
                continue;

            a_BossCell = candidates[m_Random.Next(candidates.Count)];
            return true;
        }

        return false;
    } //private bool TrySelectBossCell()

    //지정 Room 주변의 사용 가능한 Boss Room 후보 Cell 반환
    private List<Vector2Int> GetBossCandidates(
        Vector2Int a_ParentCell,
        bool a_RequireSingleNeighbour)
    {
        List<Vector2Int> candidates =
            new List<Vector2Int>(DungeonDirectionUtility.AllDirections.Length);

        foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
        {
            Vector2Int candidate =
                a_ParentCell + DungeonDirectionUtility.ToOffset(direction);

            if (m_OccupiedCells.Contains(candidate))
                continue;

            if (a_RequireSingleNeighbour &&
                CountOccupiedNeighbours(candidate) != 1)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    } //private List<Vector2Int> GetBossCandidates()

    //NormalRoom 외곽의 단일 연결 위치에 ChestRoom Cell 생성
    private void GenerateChestRoomCells()
    {
        m_ChestRoomCells.Clear();

        if (m_ChestRoomCount <= 0)
            return;

        for (int i = 0; i < m_ChestRoomCount; i++)
        {
            List<ChestRoomCandidate> candidates = GetChestRoomCandidates();

            if (candidates.Count <= 0)
            {
                Debug.LogWarning($"[DungeonGenerator] ChestRoom {i + 1}/{m_ChestRoomCount}의 유효한 생성 위치를 찾지 못했습니다.", this);
                break;
            }

            ChestRoomCandidate candidate = candidates[m_Random.Next(candidates.Count)];

            m_ChestRoomCells.Add(candidate.Cell);
            m_OccupiedCells.Add(candidate.Cell);

            Debug.Log($"[DungeonGenerator] ChestRoom 배치 : {candidate.Cell} / 연결 방 : {candidate.ParentCell}", this);
        }
    }

    //모든 NormalRoom 주변에서 단일 연결이 보장되는 ChestRoom 후보 반환
    private List<ChestRoomCandidate> GetChestRoomCandidates()
    {
        List<ChestRoomCandidate> candidates = new List<ChestRoomCandidate>();

        for (int i = 0; i < m_NormalRoomCells.Count; i++)
        {
            Vector2Int parentCell = m_NormalRoomCells[i];

            foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
            {
                Vector2Int candidateCell = parentCell + DungeonDirectionUtility.ToOffset(direction);

                if (m_OccupiedCells.Contains(candidateCell))
                    continue;

                if (CountOccupiedNeighbours(candidateCell) != 1)
                    continue;

                candidates.Add(new ChestRoomCandidate(candidateCell, parentCell));
            }
        }

        return candidates;
    }

    //현재 Layout에서 Start Cell부터 각 점유 Cell까지의 최단 거리 계산
    private Dictionary<Vector2Int, int> CalculateDistancesFromStart()
    {
        Dictionary<Vector2Int, int> distances =
            new Dictionary<Vector2Int, int>();

        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        distances[m_StartCell] = 0;
        queue.Enqueue(m_StartCell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distances[current];

            foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
            {
                Vector2Int neighbour =
                    current + DungeonDirectionUtility.ToOffset(direction);

                if (m_OccupiedCells.Contains(neighbour) == false)
                    continue;

                if (distances.ContainsKey(neighbour))
                    continue;

                distances[neighbour] = currentDistance + 1;
                queue.Enqueue(neighbour);
            }
        }

        return distances;
    } //private Dictionary<Vector2Int, int> CalculateDistancesFromStart()

    //현재 생성된 모든 Room Cell을 기준으로 연결 관계 생성
    private void BuildConnections()
    {
        m_Connections.Clear();

        HashSet<Vector2Int> layoutCells = new HashSet<Vector2Int>(m_NormalRoomCells);

        layoutCells.Add(m_StartCell);
        layoutCells.Add(m_BossCell);

        for (int i = 0; i < m_ChestRoomCells.Count; i++)
            layoutCells.Add(m_ChestRoomCells[i]);

        foreach (Vector2Int cell in layoutCells)
        {
            foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
            {
                Vector2Int neighbour = cell + DungeonDirectionUtility.ToOffset(direction);

                if (layoutCells.Contains(neighbour) == false)
                    continue;

                if (CompareCells(cell, neighbour) >= 0)
                    continue;

                m_Connections.Add(new DungeonConnection(cell, neighbour));
            }
        }
    }

    //두 Grid Cell을 안정적으로 정렬하기 위한 좌표 비교
    private int CompareCells(
        Vector2Int a_CellA,
        Vector2Int a_CellB)
    {
        if (a_CellA.x != a_CellB.x)
            return a_CellA.x.CompareTo(a_CellB.x);

        return a_CellA.y.CompareTo(a_CellB.y);
    } //private int CompareCells()

    //Start Room Prefab을 Start Cell에 생성
    private void SpawnStartRoom()
    {
        if (m_StartRoomPrefab == null)
        {
            Debug.LogError(
                $"[{name}] Start Room Prefab이 등록되지 않았습니다.",
                this
            );

            return;
        }

        GameObject roomObject = SpawnRoom(
            m_StartRoomPrefab,
            m_StartCell,
            "StartRoom"
        );

        if (roomObject == null)
            return;

        m_SpawnedStartRoom = roomObject.GetComponent<DungeonStartRoom>();

        if (m_SpawnedStartRoom == null)
        {
            Debug.LogWarning(
                $"[{roomObject.name}] DungeonStartRoom Component가 없습니다.",
                roomObject
            );
        }
    } //private void SpawnStartRoom()

    //모든 Normal Room Cell에 무작위 Room Prefab 생성
    private void SpawnNormalRooms()
    {
        List<GameObject> validPrefabs = GetValidNormalRoomPrefabs();

        if (validPrefabs.Count == 0)
        {
            Debug.LogError(
                $"[{name}] 사용할 수 있는 Normal Room Prefab이 없습니다.",
                this
            );

            return;
        }

        for (int i = 0; i < m_NormalRoomCells.Count; i++)
        {
            Vector2Int cell = m_NormalRoomCells[i];

            GameObject prefab =
                validPrefabs[m_Random.Next(validPrefabs.Count)];

            SpawnRoom(
                prefab,
                cell,
                $"NormalRoom_{i}"
            );
        }
    } //private void SpawnNormalRooms()

    //등록된 Normal Room Prefab 중 null이 아닌 Prefab만 반환
    private List<GameObject> GetValidNormalRoomPrefabs()
    {
        int capacity =
            m_NormalRoomPrefabs != null
                ? m_NormalRoomPrefabs.Count
                : 0;

        List<GameObject> validPrefabs =
            new List<GameObject>(capacity);

        if (m_NormalRoomPrefabs == null)
            return validPrefabs;

        for (int i = 0; i < m_NormalRoomPrefabs.Count; i++)
        {
            GameObject prefab = m_NormalRoomPrefabs[i];

            if (prefab != null)
                validPrefabs.Add(prefab);
        }

        return validPrefabs;
    } //private List<GameObject> GetValidNormalRoomPrefabs()

    //Boss Room Prefab을 선택된 Boss Cell에 생성
    private void SpawnBossRoom()
    {
        if (m_BossRoomPrefab == null)
        {
            Debug.LogError(
                $"[{name}] Boss Room Prefab이 등록되지 않았습니다.",
                this
            );

            return;
        }

        GameObject roomObject = SpawnRoom(
            m_BossRoomPrefab,
            m_BossCell,
            "BossRoom"
        );

        if (roomObject == null)
            return;

        m_SpawnedBossRoom = roomObject.GetComponent<DungeonBossRoom>();

        if (m_SpawnedBossRoom == null)
        {
            Debug.LogWarning(
                $"[{roomObject.name}] DungeonBossRoom Component가 없습니다.",
                roomObject
            );
        }
    } //private void SpawnBossRoom()

    //생성된 ChestRoom Cell 위치에 ChestRoom Prefab 생성
    private void SpawnChestRooms()
    {
        if (m_ChestRoomCells.Count <= 0)
            return;

        if (m_ChestRoomPrefab == null)
        {
            Debug.LogError("[DungeonGenerator] ChestRoom Prefab이 등록되지 않았습니다.", this);
            return;
        }

        for (int i = 0; i < m_ChestRoomCells.Count; i++)
        {
            Vector2Int cell = m_ChestRoomCells[i];

            SpawnRoom(
                m_ChestRoomPrefab,
                cell,
                $"ChestRoom_{i}"
            );
        }
    }

    //지정 Prefab을 Grid Cell의 World Position에 생성하고 Connection 상태 초기화
    private GameObject SpawnRoom(
        GameObject a_Prefab,
        Vector2Int a_Cell,
        string a_RoomName)
    {
        if (a_Prefab == null)
            return null;

        if (m_SpawnedRooms.ContainsKey(a_Cell))
        {
            Debug.LogWarning(
                $"[{name}] 이미 Room이 존재하는 Cell입니다. Cell={a_Cell}",
                this
            );

            return null;
        }

        Vector3 worldPosition = GetCellWorldPosition(a_Cell);

        GameObject roomObject = Instantiate(
            a_Prefab,
            worldPosition,
            Quaternion.identity,
            m_RoomRoot
        );

        if (roomObject == null)
            return null;

        roomObject.name = $"{a_RoomName}_{a_Cell.x}_{a_Cell.y}";

        m_SpawnedRooms.Add(
            a_Cell,
            roomObject
        );

        DungeonRoomConnectionController connectionController =
            roomObject.GetComponent<DungeonRoomConnectionController>();

        if (connectionController != null)
        {
            connectionController.InitializeAllDisconnected();
        }
        else
        {
            Debug.LogWarning(
                $"[{roomObject.name}] DungeonRoomConnectionController가 없습니다.",
                roomObject
            );
        }

        return roomObject;
    } //private GameObject SpawnRoom()

    //논리 Connection과 실제 Spawn Room을 기준으로 각 Room Door 연결 상태 적용
    private void ApplyRoomConnections()
    {
        foreach (KeyValuePair<Vector2Int, GameObject> pair in m_SpawnedRooms)
        {
            Vector2Int cell = pair.Key;
            GameObject roomObject = pair.Value;

            if (roomObject == null)
                continue;

            DungeonRoomConnectionController controller =
                roomObject.GetComponent<DungeonRoomConnectionController>();

            if (controller == null)
                continue;

            foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
            {
                Vector2Int neighbour =
                    cell + DungeonDirectionUtility.ToOffset(direction);

                bool connected =
                    m_SpawnedRooms.ContainsKey(neighbour) &&
                    IsConnectedCell(cell, neighbour);

                controller.SetConnected(
                    direction,
                    connected
                );
            }
        }
    } //private void ApplyRoomConnections()

    //두 Cell 사이에 방향과 관계없이 Dungeon Connection이 존재하는지 반환
    private bool IsConnectedCell(
        Vector2Int a_From,
        Vector2Int a_To)
    {
        for (int i = 0; i < m_Connections.Count; i++)
        {
            DungeonConnection connection = m_Connections[i];

            if (connection.From == a_From && connection.To == a_To)
                return true;

            if (connection.From == a_To && connection.To == a_From)
                return true;
        }

        return false;
    } //private bool IsConnectedCell()

    //Dungeon World Origin 반환
    private Vector3 GetDungeonOrigin()
    {
        return m_RoomRoot != null
            ? m_RoomRoot.position
            : transform.position;
    } //private Vector3 GetDungeonOrigin()

    //같은 GameObject에서 선택 가능한 Dungeon Component Reference 보완
    private void ResolveReferences()
    {
        if (m_CorridorGenerator == null)
            m_CorridorGenerator = GetComponent<DungeonCorridorGenerator>();
    } //private void ResolveReferences()

    //Editor에서 Start와 Boss Room Grid 크기 및 위치 표시
    private void OnDrawGizmos()
    {
        Vector3 roomGizmoSize =
            new Vector3(
                Mathf.Max(MinimumRoomSize, m_RoomSize.x),
                Mathf.Max(MinimumRoomSize, m_RoomSize.y),
                0f
            );

        Gizmos.DrawWireCube(
            GetCellWorldPosition(m_StartCell),
            roomGizmoSize
        );

        Gizmos.DrawWireCube(
            GetCellWorldPosition(m_BossCell),
            roomGizmoSize
        );
    } //private void OnDrawGizmos()
} //public class DungeonGenerator