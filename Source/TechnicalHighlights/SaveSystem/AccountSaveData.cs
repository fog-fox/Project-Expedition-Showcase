using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AccountSaveData
{
    /*
    하나의 Save Slot에 저장되는 Account 단위 영구 데이터를 보관하며
    Player 이름, Slot Metadata, 플레이 시간, 재화 및 Character별 영구 진행 상태를 관리
    */

    [SerializeField] private int m_Version = SaveVersion.Current;

    [SerializeField] private int m_SlotIndex = -1;
    [SerializeField] private string m_SaveId = string.Empty;
    [SerializeField] private string m_PlayerName = string.Empty;

    [SerializeField] private long m_CreatedUtcTicks;
    [SerializeField] private long m_LastSavedUtcTicks;
    [SerializeField, Min(0)] private int m_TotalPlayTimeSeconds;

    [SerializeField, Min(0)] private int m_Gold;

    [SerializeField] private CharicType m_LastSelectedCharicType = CharicType.Count;
    [SerializeField] private List<CharacterSaveData> m_Characters = new List<CharacterSaveData>();

    public int Version
    {
        get => m_Version;
        internal set => m_Version = value;
    }

    public int SlotIndex
    {
        get => m_SlotIndex;
        internal set => m_SlotIndex = value;
    }

    public string SaveId
    {
        get => m_SaveId;
        internal set => m_SaveId = value ?? string.Empty;
    }

    public string PlayerName
    {
        get => m_PlayerName;
        internal set => m_PlayerName = value ?? string.Empty;
    }

    public long CreatedUtcTicks
    {
        get => m_CreatedUtcTicks;
        internal set => m_CreatedUtcTicks = value;
    }

    public long LastSavedUtcTicks
    {
        get => m_LastSavedUtcTicks;
        internal set => m_LastSavedUtcTicks = value;
    }

    public int TotalPlayTimeSeconds
    {
        get => m_TotalPlayTimeSeconds;
        internal set => m_TotalPlayTimeSeconds = Mathf.Max(0, value);
    }

    public int Gold
    {
        get => m_Gold;
        internal set => m_Gold = Mathf.Max(0, value);
    }

    public CharicType LastSelectedCharicType
    {
        get => m_LastSelectedCharicType;
        internal set => m_LastSelectedCharicType = value;
    }

    public IReadOnlyList<CharacterSaveData> Characters => m_Characters;

    internal List<CharacterSaveData> MutableCharacters => m_Characters;
} //public class AccountSaveData