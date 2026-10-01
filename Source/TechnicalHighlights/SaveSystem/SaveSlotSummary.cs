public struct SaveSlotSummary
{
    /*
    SaveSelect UI에서 하나의 Slot 상태와 Player 및 주요 저장 정보를 표시하기 위한
    Runtime 전용 Save Data 요약 정보
    */

    public int SlotIndex { get; }
    public SaveSlotState State { get; }
    public int Version { get; }
    public string SaveId { get; }
    public string PlayerName { get; }
    public CharicType LastSelectedCharicType { get; }
    public int TotalPlayTimeSeconds { get; }
    public long LastSavedUtcTicks { get; }

    public bool HasData => State != SaveSlotState.Empty;
    public bool CanLoad => State == SaveSlotState.Valid;

    //지정 Save Slot의 요약 정보를 생성
    public SaveSlotSummary(
        int a_SlotIndex,
        SaveSlotState a_State,
        int a_Version,
        string a_SaveId,
        string a_PlayerName,
        CharicType a_LastSelectedCharicType,
        int a_TotalPlayTimeSeconds,
        long a_LastSavedUtcTicks)
    {
        SlotIndex = a_SlotIndex;
        State = a_State;
        Version = a_Version;
        SaveId = a_SaveId ?? string.Empty;
        PlayerName = a_PlayerName ?? string.Empty;
        LastSelectedCharicType = a_LastSelectedCharicType;
        TotalPlayTimeSeconds = a_TotalPlayTimeSeconds >= 0 ? a_TotalPlayTimeSeconds : 0;
        LastSavedUtcTicks = a_LastSavedUtcTicks;
    } //public SaveSlotSummary()

    //Save 파일이 존재하지 않는 빈 Slot 요약 정보 생성
    public static SaveSlotSummary CreateEmpty(int a_SlotIndex)
    {
        return new SaveSlotSummary(
            a_SlotIndex,
            SaveSlotState.Empty,
            SaveVersion.Current,
            string.Empty,
            string.Empty,
            CharicType.Count,
            0,
            0
        );
    } //public static SaveSlotSummary CreateEmpty()
} //public struct SaveSlotSummary