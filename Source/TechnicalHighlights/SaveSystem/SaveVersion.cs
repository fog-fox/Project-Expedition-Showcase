/*
Save File Format의 현재 버전과 지원 가능한 최소 버전을 정의하고
로드할 Save Data의 버전 호환 여부를 판단
*/
public static class SaveVersion
{
    public const int V1 = 1;

    public const int Current = V1;
    public const int MinimumSupported = V1;

    //지정 Save Version이 현재 게임에서 지원 가능한 범위인지 반환
    public static bool IsSupported(int a_Version)
    {
        return a_Version >= MinimumSupported && a_Version <= Current;
    } //public static bool IsSupported()

    //지정 Save Version이 현재 게임보다 새로운 버전인지 반환
    public static bool IsNewerThanCurrent(int a_Version)
    {
        return a_Version > Current;
    } //public static bool IsNewerThanCurrent()
} //public static class SaveVersion