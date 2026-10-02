public enum SaveOperationState
{
    //현재 저장 작업이 없는 상태
    Idle = 0,

    //파일 저장을 시도하고 있는 상태
    Saving = 1,

    //파일 저장이 정상적으로 완료된 상태
    Succeeded = 2,

    //파일 저장에 실패한 상태
    Failed = 3
} //public enum SaveOperationState