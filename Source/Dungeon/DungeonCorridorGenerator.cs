using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonCorridorGenerator : MonoBehaviour
{
    /*
    DungeonGenerator가 생성한 Room Connection 정보를 기반으로
    인접한 Room 사이에 직선 Corridor를 생성하고 Runtime Corridor를 관리
    */

    private const float MinimumCorridorWidth = 0.1f;

    [Header("Dungeon")]
    [FormerlySerializedAs("dungeonGenerator")]
    [SerializeField] private DungeonGenerator m_DungeonGenerator;

    [Header("Corridor Prefab")]
    [FormerlySerializedAs("corridorPrefab")]
    [SerializeField] private DungeonCorridor m_CorridorPrefab;

    [Header("Corridor Setting")]
    [FormerlySerializedAs("corridorWidth")]
    [SerializeField, Min(MinimumCorridorWidth)] private float m_CorridorWidth = 3f;

    [Header("Hierarchy")]
    [FormerlySerializedAs("corridorRoot")]
    [SerializeField] private Transform m_CorridorRoot;

    private readonly List<DungeonCorridor> m_SpawnedCorridors =
        new List<DungeonCorridor>();

    //DungeonGenerator Reference 보완
    private void Awake()
    {
        ResolveReferences();
    } //private void Awake()

    //Inspector에서 Reference와 Corridor 설정값 보정
    private void OnValidate()
    {
        ResolveReferences();
        m_CorridorWidth = Mathf.Max(MinimumCorridorWidth, m_CorridorWidth);
    } //private void OnValidate()

    //현재 Dungeon Connection 정보를 기준으로 모든 Corridor 생성
    public void GenerateCorridors()
    {
        if (m_DungeonGenerator == null)
        {
            Debug.LogError(
                $"[{name}] DungeonGenerator가 없습니다.",
                this
            );

            return;
        }

        if (m_CorridorPrefab == null)
        {
            Debug.LogError(
                $"[{name}] Corridor Prefab이 없습니다.",
                this
            );

            return;
        }

        ClearCorridors();

        IReadOnlyList<DungeonConnection> connections = m_DungeonGenerator.Connections;

        if (connections == null || connections.Count == 0)
            return;

        for (int i = 0; i < connections.Count; i++)
            TryCreateCorridor(connections[i]);
    } //public void GenerateCorridors()

    //하나의 Dungeon Connection을 실제 Corridor로 생성 시도
    private void TryCreateCorridor(DungeonConnection a_Connection)
    {
        Vector2Int fromCell = a_Connection.From;
        Vector2Int toCell = a_Connection.To;

        /*
        현재 단계에서는 DungeonGenerator가 실제로 생성한
        두 Room 사이의 Connection만 Corridor로 만듭니다.
        */
        if (m_DungeonGenerator.TryGetSpawnedRoom(fromCell, out GameObject fromRoom) == false ||
            m_DungeonGenerator.TryGetSpawnedRoom(toCell, out GameObject toRoom) == false)
        {
            return;
        }

        if (fromRoom == null || toRoom == null)
            return;

        Vector2Int cellDelta = toCell - fromCell;
        int distance = Mathf.Abs(cellDelta.x) + Mathf.Abs(cellDelta.y);

        if (distance != 1)
        {
            Debug.LogWarning(
                $"[{name}] 인접하지 않은 Room Connection입니다. {fromCell} -> {toCell}",
                this
            );

            return;
        }

        Vector3 fromPosition = m_DungeonGenerator.GetCellWorldPosition(fromCell);
        Vector3 toPosition = m_DungeonGenerator.GetCellWorldPosition(toCell);
        Vector3 corridorPosition = (fromPosition + toPosition) * 0.5f;

        DungeonCorridorOrientation orientation;
        float corridorLength;

        if (cellDelta.x != 0)
        {
            orientation = DungeonCorridorOrientation.Horizontal;
            corridorLength =
                Mathf.Abs(toPosition.x - fromPosition.x) -
                m_DungeonGenerator.RoomSize.x;
        }
        else
        {
            orientation = DungeonCorridorOrientation.Vertical;
            corridorLength =
                Mathf.Abs(toPosition.y - fromPosition.y) -
                m_DungeonGenerator.RoomSize.y;
        }

        if (corridorLength <= 0f)
        {
            Debug.LogWarning(
                $"[{name}] Corridor 길이가 올바르지 않습니다. " +
                $"{fromCell} -> {toCell}, Length={corridorLength:0.###}",
                this
            );

            return;
        }

        DungeonCorridor corridor = Instantiate(
            m_CorridorPrefab,
            corridorPosition,
            Quaternion.identity,
            m_CorridorRoot
        );

        if (corridor == null)
            return;

        corridor.name =
            $"Corridor_{fromCell.x}_{fromCell.y}_To_{toCell.x}_{toCell.y}";

        corridor.Configure(
            orientation,
            corridorLength,
            Mathf.Max(MinimumCorridorWidth, m_CorridorWidth)
        );

        m_SpawnedCorridors.Add(corridor);
    } //private void TryCreateCorridor()

    //현재 Generator가 생성하여 관리하는 모든 Corridor 제거
    public void ClearCorridors()
    {
        for (int i = m_SpawnedCorridors.Count - 1; i >= 0; i--)
        {
            DungeonCorridor corridor = m_SpawnedCorridors[i];

            if (corridor == null)
                continue;

            /*
            Destroy는 Frame 종료까지 지연되므로 같은 Frame에 다시 생성할 경우
            기존 Corridor Collider가 남지 않도록 먼저 비활성화합니다.
            */
            corridor.gameObject.SetActive(false);
            Destroy(corridor.gameObject);
        }

        m_SpawnedCorridors.Clear();
    } //public void ClearCorridors()

    //같은 GameObject에서 DungeonGenerator Reference 자동 탐색
    private void ResolveReferences()
    {
        if (m_DungeonGenerator == null)
            m_DungeonGenerator = GetComponent<DungeonGenerator>();
    } //private void ResolveReferences()
} //public class DungeonCorridorGenerator