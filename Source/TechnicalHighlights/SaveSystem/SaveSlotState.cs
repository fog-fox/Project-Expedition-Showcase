public enum SaveSlotState
{
    //해당 Slot에 Save 파일이 없는 상태
    Empty = 0,

    //현재 게임에서 정상적으로 읽을 수 있는 Save 상태
    Valid = 1,

    //파일은 존재하지만 정상적으로 읽거나 검증할 수 없는 상태
    Corrupted = 2,

    //Save Version이 현재 게임보다 새로워 읽을 수 없는 상태
    UnsupportedVersion = 3
} //public enum SaveSlotState