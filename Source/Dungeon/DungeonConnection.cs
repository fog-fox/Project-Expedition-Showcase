using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct DungeonConnection
{
    /*
    Dungeon Grid의 두 Cell 사이에 존재하는
    하나의 연결 관계를 직렬화하여 저장
    */

    [FormerlySerializedAs("from")]
    [SerializeField] private Vector2Int m_From;

    [FormerlySerializedAs("to")]
    [SerializeField] private Vector2Int m_To;

    public Vector2Int From => m_From;
    public Vector2Int To => m_To;

    //두 Dungeon Cell을 연결하는 Connection 생성
    public DungeonConnection(Vector2Int a_From, Vector2Int a_To)
    {
        m_From = a_From;
        m_To = a_To;
    } //public DungeonConnection()

    //지정 Cell이 현재 Connection의 양 끝점 중 하나인지 반환
    public bool Contains(Vector2Int a_Cell)
    {
        return m_From == a_Cell || m_To == a_Cell;
    } //public bool Contains()
} //public struct DungeonConnection