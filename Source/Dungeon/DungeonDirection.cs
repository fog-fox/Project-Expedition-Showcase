using UnityEngine;

public enum DungeonDirection
{
    //위쪽 Cell 방향
    Up = 0,

    //아래쪽 Cell 방향
    Down = 1,

    //왼쪽 Cell 방향
    Left = 2,

    //오른쪽 Cell 방향
    Right = 3
}

public static class DungeonDirectionUtility
{
    /*
    Dungeon Grid에서 사용하는 방향 정보를 제공하고
    Direction과 Cell Offset 변환 및 반대 방향 계산을 담당
    */

    public static readonly DungeonDirection[] AllDirections =
    {
        DungeonDirection.Up,
        DungeonDirection.Down,
        DungeonDirection.Left,
        DungeonDirection.Right
    };

    //Dungeon Direction을 Grid Cell 이동 Offset으로 변환
    public static Vector2Int ToOffset(DungeonDirection a_Direction)
    {
        switch (a_Direction)
        {
            case DungeonDirection.Up:
                return Vector2Int.up;

            case DungeonDirection.Down:
                return Vector2Int.down;

            case DungeonDirection.Left:
                return Vector2Int.left;

            case DungeonDirection.Right:
                return Vector2Int.right;

            default:
                return Vector2Int.zero;
        }
    } //public static Vector2Int ToOffset()

    //지정 Dungeon Direction의 반대 방향 반환
    public static DungeonDirection GetOpposite(DungeonDirection a_Direction)
    {
        switch (a_Direction)
        {
            case DungeonDirection.Up:
                return DungeonDirection.Down;

            case DungeonDirection.Down:
                return DungeonDirection.Up;

            case DungeonDirection.Left:
                return DungeonDirection.Right;

            case DungeonDirection.Right:
                return DungeonDirection.Left;

            default:
                return a_Direction;
        }
    } //public static DungeonDirection GetOpposite()
} //public static class DungeonDirectionUtility