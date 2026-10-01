using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonRoomConnectionController : MonoBehaviour
{
    /*
    Dungeon Room의 상하좌우 Door Reference를 관리하고
    DungeonGenerator가 결정한 Connection 상태를 실제 Door에 적용
    */

    [Header("Doors")]
    [FormerlySerializedAs("upDoor")]
    [SerializeField] private DungeonRoomDoor m_UpDoor;

    [FormerlySerializedAs("downDoor")]
    [SerializeField] private DungeonRoomDoor m_DownDoor;

    [FormerlySerializedAs("leftDoor")]
    [SerializeField] private DungeonRoomDoor m_LeftDoor;

    [FormerlySerializedAs("rightDoor")]
    [SerializeField] private DungeonRoomDoor m_RightDoor;

    //Inspector에서 동일한 Door가 여러 방향에 중복 등록되었는지 검사
    private void OnValidate()
    {
        ValidateDoorAssignments();
    } //private void OnValidate()

    //모든 방향의 Door를 연결되지 않은 상태로 초기화
    public void InitializeAllDisconnected()
    {
        foreach (DungeonDirection direction in DungeonDirectionUtility.AllDirections)
            SetConnected(direction, false);
    } //public void InitializeAllDisconnected()

    //지정 방향 Door에 Dungeon 연결 상태 적용
    public void SetConnected(
        DungeonDirection a_Direction,
        bool a_IsConnected)
    {
        DungeonRoomDoor door = GetDoor(a_Direction);

        if (door == null)
            return;

        door.SetConnected(a_IsConnected);
    } //public void SetConnected()

    //지정 Dungeon Direction에 대응하는 Door 반환
    public DungeonRoomDoor GetDoor(DungeonDirection a_Direction)
    {
        switch (a_Direction)
        {
            case DungeonDirection.Up:
                return m_UpDoor;

            case DungeonDirection.Down:
                return m_DownDoor;

            case DungeonDirection.Left:
                return m_LeftDoor;

            case DungeonDirection.Right:
                return m_RightDoor;

            default:
                return null;
        }
    } //public DungeonRoomDoor GetDoor()

    //하나의 Door Reference가 둘 이상의 방향에 등록되었는지 검사
    private void ValidateDoorAssignments()
    {
        ValidateDoorPair(
            m_UpDoor,
            DungeonDirection.Up,
            m_DownDoor,
            DungeonDirection.Down
        );

        ValidateDoorPair(
            m_UpDoor,
            DungeonDirection.Up,
            m_LeftDoor,
            DungeonDirection.Left
        );

        ValidateDoorPair(
            m_UpDoor,
            DungeonDirection.Up,
            m_RightDoor,
            DungeonDirection.Right
        );

        ValidateDoorPair(
            m_DownDoor,
            DungeonDirection.Down,
            m_LeftDoor,
            DungeonDirection.Left
        );

        ValidateDoorPair(
            m_DownDoor,
            DungeonDirection.Down,
            m_RightDoor,
            DungeonDirection.Right
        );

        ValidateDoorPair(
            m_LeftDoor,
            DungeonDirection.Left,
            m_RightDoor,
            DungeonDirection.Right
        );
    } //private void ValidateDoorAssignments()

    //두 방향에 동일한 Door가 등록된 경우 Inspector 설정 오류 경고
    private void ValidateDoorPair(
        DungeonRoomDoor a_FirstDoor,
        DungeonDirection a_FirstDirection,
        DungeonRoomDoor a_SecondDoor,
        DungeonDirection a_SecondDirection)
    {
        if (a_FirstDoor == null || a_SecondDoor == null)
            return;

        if (a_FirstDoor != a_SecondDoor)
            return;

        Debug.LogWarning(
            $"[{name}] 동일한 DungeonRoomDoor가 " +
            $"{a_FirstDirection}과 {a_SecondDirection} 방향에 중복 등록되어 있습니다.",
            this
        );
    } //private void ValidateDoorPair()
} //public class DungeonRoomConnectionController