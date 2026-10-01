using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonBossRoom : MonoBehaviour
{
    /*
    Boss Room의 Clear 상태를 감시하고
    설정에 따라 Exit Portal의 활성 상태를 관리
    */

    [Header("Room")]
    [FormerlySerializedAs("room")]
    [SerializeField] private DungeonRoom m_Room;

    [Header("Exit")]
    [FormerlySerializedAs("exitPortal")]
    [SerializeField] private GameObject m_ExitPortal;

    [Header("Exit Setting")]
    [FormerlySerializedAs("activatePortalOnClear")]
    [SerializeField] private bool m_ActivatePortalOnClear = true;

    public GameObject ExitPortal => m_ExitPortal;

    //DungeonRoom Reference를 보완
    private void Awake()
    {
        ResolveRoom();
    } //private void Awake()

    //Room Clear Event를 구독하고 현재 Portal 상태 갱신
    private void OnEnable()
    {
        ResolveRoom();

        if (m_Room != null)
            m_Room.RoomCleared += HandleRoomCleared;

        RefreshPortalState();
    } //private void OnEnable()

    //Room Clear Event 구독 해제
    private void OnDisable()
    {
        if (m_Room != null)
            m_Room.RoomCleared -= HandleRoomCleared;
    } //private void OnDisable()

    //Inspector Reference를 보완하고 Portal 계층 구조 검사
    private void OnValidate()
    {
        ResolveRoom();
        ValidatePortalHierarchy();
    } //private void OnValidate()

    //연결된 Boss Room이 Clear되었을 때 Portal 상태 갱신
    private void HandleRoomCleared(DungeonRoom a_ClearedRoom)
    {
        if (a_ClearedRoom != m_Room)
            return;

        RefreshPortalState();
    } //private void HandleRoomCleared()

    //현재 Room Clear 상태와 설정에 따라 Exit Portal 활성 상태 갱신
    private void RefreshPortalState()
    {
        if (m_ExitPortal == null)
            return;

        bool shouldActivate =
            m_ActivatePortalOnClear == false ||
            (m_Room != null && m_Room.IsCleared);

        if (m_ExitPortal.activeSelf != shouldActivate)
            m_ExitPortal.SetActive(shouldActivate);
    } //private void RefreshPortalState()

    //같은 GameObject에서 DungeonRoom Reference 탐색
    private void ResolveRoom()
    {
        if (m_Room == null)
            m_Room = GetComponent<DungeonRoom>();
    } //private void ResolveRoom()

    //Portal 비활성화 시 DungeonBossRoom까지 함께 꺼지는 잘못된 구조인지 검사
    private void ValidatePortalHierarchy()
    {
        if (m_ExitPortal == null)
            return;

        Transform portalTransform = m_ExitPortal.transform;

        if (portalTransform == transform ||
            transform.IsChildOf(portalTransform))
        {
            Debug.LogWarning(
                $"[{name}] Exit Portal이 DungeonBossRoom 자신 또는 Parent로 설정되어 있습니다. " +
                "Portal 비활성화 시 DungeonBossRoom도 비활성화되어 RoomCleared Event를 받을 수 없습니다.",
                this
            );
        }
    } //private void ValidatePortalHierarchy()
} //public class DungeonBossRoom