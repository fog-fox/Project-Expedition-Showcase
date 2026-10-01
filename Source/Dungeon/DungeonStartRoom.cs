using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonStartRoom : MonoBehaviour
{
    /*
    Dungeon Start Room의 Player Spawn 기준점을 보관하고
    Spawn Position과 Rotation 정보를 외부 시스템에 제공
    */

    [Header("Player Spawn")]
    [FormerlySerializedAs("playerSpawnPoint")]
    [SerializeField] private Transform m_PlayerSpawnPoint;

    public Transform PlayerSpawnPoint => m_PlayerSpawnPoint;

    public Vector3 PlayerSpawnPosition =>
        m_PlayerSpawnPoint != null
            ? m_PlayerSpawnPoint.position
            : transform.position;

    public Quaternion PlayerSpawnRotation =>
        m_PlayerSpawnPoint != null
            ? m_PlayerSpawnPoint.rotation
            : transform.rotation;
} //public class DungeonStartRoom