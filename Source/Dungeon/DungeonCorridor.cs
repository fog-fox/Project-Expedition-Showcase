using UnityEngine;
using UnityEngine.Serialization;

public enum DungeonCorridorOrientation
{
    //X축 방향으로 연결되는 Corridor
    Horizontal = 0,

    //Y축 방향으로 연결되는 Corridor
    Vertical = 1
}

[DisallowMultipleComponent]
public class DungeonCorridor : MonoBehaviour
{
    /*
    Dungeon Generator가 생성한 직선 Corridor의 방향과 크기를 받아
    Floor Sprite와 양쪽 Wall Collider의 위치 및 크기를 구성
    */

    private const float MinimumSize = 0.01f;
    private const float MinimumWallThickness = 0.01f;

    [Header("Visual")]
    [FormerlySerializedAs("floorRenderer")]
    [SerializeField] private SpriteRenderer m_FloorRenderer;

    [Header("Wall Collision")]
    [FormerlySerializedAs("wallA")]
    [SerializeField] private BoxCollider2D m_WallA;

    [FormerlySerializedAs("wallB")]
    [SerializeField] private BoxCollider2D m_WallB;

    [Header("Wall Setting")]
    [FormerlySerializedAs("wallThickness")]
    [SerializeField, Min(MinimumWallThickness)] private float m_WallThickness = 0.5f;

    //Inspector에서 Wall Thickness의 유효 범위 보정
    private void OnValidate()
    {
        m_WallThickness = Mathf.Max(MinimumWallThickness, m_WallThickness);
    } //private void OnValidate()

    //Corridor 방향과 길이 및 폭을 기준으로 Floor와 Wall 구성
    public void Configure(
        DungeonCorridorOrientation a_Orientation,
        float a_Length,
        float a_Width)
    {
        float length = Mathf.Max(MinimumSize, a_Length);
        float width = Mathf.Max(MinimumSize, a_Width);

        ConfigureFloor(
            a_Orientation,
            length,
            width
        );

        ConfigureWalls(
            a_Orientation,
            length,
            width
        );
    } //public void Configure()

    //Corridor 방향에 맞춰 Floor Sprite 크기 설정
    private void ConfigureFloor(
        DungeonCorridorOrientation a_Orientation,
        float a_Length,
        float a_Width)
    {
        if (m_FloorRenderer == null)
            return;

        m_FloorRenderer.drawMode = SpriteDrawMode.Tiled;

        m_FloorRenderer.size =
            a_Orientation == DungeonCorridorOrientation.Horizontal
                ? new Vector2(a_Length, a_Width)
                : new Vector2(a_Width, a_Length);
    } //private void ConfigureFloor()

    //Corridor 방향에 따라 양쪽 Wall Collider 구성
    private void ConfigureWalls(
        DungeonCorridorOrientation a_Orientation,
        float a_Length,
        float a_Width)
    {
        float wallThickness = Mathf.Max(MinimumWallThickness, m_WallThickness);

        if (a_Orientation == DungeonCorridorOrientation.Horizontal)
        {
            ConfigureHorizontalWalls(
                a_Length,
                a_Width,
                wallThickness
            );
        }
        else
        {
            ConfigureVerticalWalls(
                a_Length,
                a_Width,
                wallThickness
            );
        }
    } //private void ConfigureWalls()

    //수평 Corridor의 위쪽과 아래쪽 Wall Collider 구성
    private void ConfigureHorizontalWalls(
        float a_Length,
        float a_Width,
        float a_WallThickness)
    {
        float wallOffset = a_Width * 0.5f + a_WallThickness * 0.5f;
        Vector2 wallSize = new Vector2(a_Length, a_WallThickness);

        ConfigureWall(
            m_WallA,
            new Vector2(0f, wallOffset),
            wallSize
        );

        ConfigureWall(
            m_WallB,
            new Vector2(0f, -wallOffset),
            wallSize
        );
    } //private void ConfigureHorizontalWalls()

    //수직 Corridor의 왼쪽과 오른쪽 Wall Collider 구성
    private void ConfigureVerticalWalls(
        float a_Length,
        float a_Width,
        float a_WallThickness)
    {
        float wallOffset = a_Width * 0.5f + a_WallThickness * 0.5f;
        Vector2 wallSize = new Vector2(a_WallThickness, a_Length);

        ConfigureWall(
            m_WallA,
            new Vector2(wallOffset, 0f),
            wallSize
        );

        ConfigureWall(
            m_WallB,
            new Vector2(-wallOffset, 0f),
            wallSize
        );
    } //private void ConfigureVerticalWalls()

    //개별 Wall Collider의 Local Position과 Size 설정
    private void ConfigureWall(
        BoxCollider2D a_Wall,
        Vector2 a_LocalPosition,
        Vector2 a_Size)
    {
        if (a_Wall == null)
            return;

        a_Wall.transform.localPosition = a_LocalPosition;
        a_Wall.size = a_Size;
    } //private void ConfigureWall()
} //public class DungeonCorridor