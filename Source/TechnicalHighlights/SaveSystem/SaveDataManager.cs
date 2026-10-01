using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class SaveDataManager : MonoBehaviour
{
    /*
    Account Save의 생성, 로드, 저장, 삭제 및 Slot 상태 검증을 담당하고
    Main, Backup, Temporary 파일을 이용하여 저장 데이터 손상에 대응
    */

    public const int SlotCount = 3;

    private const string SaveDirectoryName = "SaveData";
    private const string SaveFilePrefix = "slot_";
    private const string MainSaveExtension = ".json";
    private const string BackupSaveExtension = ".bak";
    private const string TemporarySaveExtension = ".tmp";

    private static readonly Encoding s_Utf8NoBom = new UTF8Encoding(false);
    private static SaveDataManager s_Instance;

    private AccountSaveData m_ActiveSaveData;
    private int m_ActiveSlotIndex = -1;

    private double m_LastPlayTimeCheckpoint;
    private double m_PlayTimeRemainderSeconds;

    private SaveDirtyFlags m_DirtyFlags = SaveDirtyFlags.None;

    private Player m_BoundPlayer;
    private CurrencySession m_BoundCurrencySession;
    private ICharicSkillLoadoutProvider m_BoundSkillLoadoutProvider;

    private bool m_IsApplyingSaveData;

    public event Action<SaveOperationState> OnSaveOperationStateChanged;

    public bool IsDirty => m_DirtyFlags != SaveDirtyFlags.None;
    public SaveDirtyFlags DirtyFlags => m_DirtyFlags;

    public static SaveDataManager Instance
    {
        get
        {
            if (s_Instance != null)
                return s_Instance;

            SaveDataManager foundManager = FindAnyObjectByType<SaveDataManager>();

            if (foundManager != null)
            {
                s_Instance = foundManager;
                s_Instance.InitializeSingleton();
                return s_Instance;
            }

            GameObject managerObject = new GameObject(nameof(SaveDataManager));
            return managerObject.AddComponent<SaveDataManager>();
        }
    }

    public bool HasActiveSave => m_ActiveSaveData != null && IsSlotIndexValid(m_ActiveSlotIndex);
    public int ActiveSlotIndex => m_ActiveSlotIndex;
    public AccountSaveData ActiveSaveData => m_ActiveSaveData;
    public string SaveDirectoryPath => GetSaveDirectoryPath();

    //Domain Reload 비활성 환경에서도 이전 Singleton Reference 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_Instance = null;
    } //private static void ResetStaticState()

    //Singleton을 초기화하고 Save Directory를 준비
    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;

        InitializeSingleton();
        EnsureSaveDirectory();
    } //private void Awake()

    //현재 Object가 Singleton이면 Static Reference와 Runtime 상태 정리
    private void OnDestroy()
    {
        if (s_Instance != this)
            return;

        ResetPlayTimeTracking();
        UnbindRuntime();

        s_Instance = null;
    } //private void OnDestroy()

    //SaveDataManager를 Scene 전환 사이에서 유지
    private void InitializeSingleton()
    {
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    } //private void InitializeSingleton()

    //지정 Slot의 현재 저장 상태와 UI용 요약 정보 반환
    public SaveSlotSummary GetSlotSummary(int a_SlotIndex)
    {
        if (IsSlotIndexValid(a_SlotIndex) == false)
        {
            Debug.LogWarning($"유효하지 않은 Save Slot Index입니다: {a_SlotIndex}", this);
            return SaveSlotSummary.CreateEmpty(a_SlotIndex);
        }

        SaveSlotState state = ResolveSlotData(a_SlotIndex, out AccountSaveData data, out _);

        if (state == SaveSlotState.Empty)
            return SaveSlotSummary.CreateEmpty(a_SlotIndex);

        if (data == null)
        {
            return new SaveSlotSummary(
                a_SlotIndex,
                state,
                0,
                string.Empty,
                string.Empty,
                CharicType.Count,
                0,
                0
            );
        }

        return CreateSlotSummary(data, state);
    } //public SaveSlotSummary GetSlotSummary()

    //지정 Slot의 Save Data를 읽어 현재 Active Save로 설정
    public bool TryLoadSlot(int a_SlotIndex)
    {
        if (IsSlotIndexValid(a_SlotIndex) == false)
        {
            Debug.LogWarning($"유효하지 않은 Save Slot Index입니다: {a_SlotIndex}", this);
            return false;
        }

        if (HasActiveSave && m_ActiveSlotIndex != a_SlotIndex)
        {
            Debug.LogWarning($"다른 Save Slot {m_ActiveSlotIndex}이 이미 활성화되어 있습니다.", this);
            return false;
        }

        SaveSlotState state = ResolveSlotData(a_SlotIndex, out AccountSaveData data, out string sourcePath);

        if (state != SaveSlotState.Valid || data == null)
        {
            Debug.LogWarning($"Save Slot {a_SlotIndex}을 로드할 수 없습니다. State = {state}", this);
            return false;
        }

        string mainPath = GetMainSavePath(a_SlotIndex);

        if (sourcePath != mainPath)
            TryRestoreMainFile(a_SlotIndex, sourcePath);

        m_ActiveSaveData = data;
        m_ActiveSlotIndex = a_SlotIndex;

        StartPlayTimeTracking();

        return true;
    } //public bool TryLoadSlot()

    //새 Account Save를 생성하고 지정 Slot의 최초 파일로 저장
    public bool CreateNewSlot(int a_SlotIndex, string a_PlayerName, CharicType a_CharicType, int a_InitialGold = 0)
    {
        if (IsSlotIndexValid(a_SlotIndex) == false)
        {
            Debug.LogWarning($"유효하지 않은 Save Slot Index입니다: {a_SlotIndex}", this);
            return false;
        }

        string playerName = a_PlayerName != null ? a_PlayerName.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            Debug.LogWarning("Player 이름이 비어 있어 Save를 생성할 수 없습니다.", this);
            return false;
        }

        if (IsCharicTypeValid(a_CharicType) == false)
        {
            Debug.LogWarning($"유효하지 않은 Character Type입니다: {a_CharicType}", this);
            return false;
        }

        if (HasActiveSave)
        {
            Debug.LogWarning("이미 활성화된 Save Data가 있어 새 Slot을 생성할 수 없습니다.", this);
            return false;
        }

        if (HasAnySlotFile(a_SlotIndex))
        {
            Debug.LogWarning($"Save Slot {a_SlotIndex}에 기존 파일이 존재합니다.", this);
            return false;
        }

        long nowTicks = DateTime.UtcNow.Ticks;

        AccountSaveData saveData = new AccountSaveData
        {
            Version = SaveVersion.Current,
            SlotIndex = a_SlotIndex,
            SaveId = Guid.NewGuid().ToString("N"),
            PlayerName = playerName,
            CreatedUtcTicks = nowTicks,
            LastSavedUtcTicks = nowTicks,
            TotalPlayTimeSeconds = 0,
            Gold = Mathf.Max(0, a_InitialGold),
            LastSelectedCharicType = a_CharicType
        };

        CharacterSaveData characterSaveData = new CharacterSaveData
        {
            CharicType = a_CharicType
        };

        saveData.MutableCharacters.Add(characterSaveData);

        NotifySaveOperationState(SaveOperationState.Saving);

        if (TryWriteSaveFile(saveData) == false)
        {
            NotifySaveOperationState(SaveOperationState.Failed);
            return false;
        }

        m_ActiveSaveData = saveData;
        m_ActiveSlotIndex = a_SlotIndex;

        ResetDirtyState();
        StartPlayTimeTracking();

        NotifySaveOperationState(SaveOperationState.Succeeded);

        return true;
    } //public bool CreateNewSlot()

    //현재 Active Save의 플레이 시간을 반영하고 파일로 저장
    public bool SaveActiveSlot()
    {
        if (HasActiveSave == false)
        {
            Debug.LogWarning("저장할 Active Save Data가 없습니다.", this);
            return false;
        }

        SaveSlotState validationState = ValidateSaveData(m_ActiveSaveData, m_ActiveSlotIndex);

        if (validationState != SaveSlotState.Valid)
        {
            Debug.LogError($"Active Save Data가 유효하지 않아 저장할 수 없습니다. State = {validationState}", this);
            NotifySaveOperationState(SaveOperationState.Failed);
            return false;
        }

        UpdateTrackedPlayTime();

        long previousLastSavedTicks = m_ActiveSaveData.LastSavedUtcTicks;
        m_ActiveSaveData.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

        NotifySaveOperationState(SaveOperationState.Saving);

        if (TryWriteSaveFile(m_ActiveSaveData))
        {
            ResetDirtyState();
            NotifySaveOperationState(SaveOperationState.Succeeded);
            return true;
        }

        m_ActiveSaveData.LastSavedUtcTicks = previousLastSavedTicks;

        NotifySaveOperationState(SaveOperationState.Failed);

        return false;
    } //public bool SaveActiveSlot()

    //Save 작업 상태 변경 Event 전달
    private void NotifySaveOperationState(SaveOperationState a_State)
    {
        OnSaveOperationStateChanged?.Invoke(a_State);
    } //private void NotifySaveOperationState()

    //지정 Slot의 Main, Backup, Temporary 파일을 모두 삭제
    public bool DeleteSlot(int a_SlotIndex)
    {
        if (IsSlotIndexValid(a_SlotIndex) == false)
        {
            Debug.LogWarning($"유효하지 않은 Save Slot Index입니다: {a_SlotIndex}", this);
            return false;
        }

        bool success = true;

        success &= TryDeleteFile(GetMainSavePath(a_SlotIndex));
        success &= TryDeleteFile(GetBackupSavePath(a_SlotIndex));
        success &= TryDeleteFile(GetTemporarySavePath(a_SlotIndex));

        if (success == false)
        {
            Debug.LogError($"Save Slot {a_SlotIndex}의 파일을 완전히 삭제하지 못했습니다.", this);
            return false;
        }

        if (m_ActiveSlotIndex == a_SlotIndex)
            ClearActiveSave();

        return true;
    } //public bool DeleteSlot()

    //현재 Active Save Reference와 Runtime Event 및 플레이 시간 추적 상태 해제
    public void ClearActiveSave()
    {
        UnbindRuntime();

        m_ActiveSaveData = null;
        m_ActiveSlotIndex = -1;

        ResetDirtyState();
        ResetPlayTimeTracking();
    } //public void ClearActiveSave()

    //Account Save Data를 SaveSlotSummary로 변환
    private SaveSlotSummary CreateSlotSummary(AccountSaveData a_Data, SaveSlotState a_State)
    {
        return new SaveSlotSummary(
            a_Data.SlotIndex,
            a_State,
            a_Data.Version,
            a_Data.SaveId,
            a_Data.PlayerName,
            a_Data.LastSelectedCharicType,
            a_Data.TotalPlayTimeSeconds,
            a_Data.LastSavedUtcTicks
        );
    } //private SaveSlotSummary CreateSlotSummary()

    //Main, Backup, Temporary 파일 상태를 확인하고 사용할 수 있는 가장 적절한 Save Data 반환
    private SaveSlotState ResolveSlotData(int a_SlotIndex, out AccountSaveData a_Data, out string a_SourcePath)
    {
        a_Data = null;
        a_SourcePath = string.Empty;

        string mainPath = GetMainSavePath(a_SlotIndex);
        string backupPath = GetBackupSavePath(a_SlotIndex);
        string temporaryPath = GetTemporarySavePath(a_SlotIndex);

        SaveSlotState mainState = ReadSaveFileState(mainPath, a_SlotIndex, out AccountSaveData mainData);

        if (mainState == SaveSlotState.Valid)
        {
            a_Data = mainData;
            a_SourcePath = mainPath;
            return SaveSlotState.Valid;
        }

        if (mainState == SaveSlotState.UnsupportedVersion)
        {
            a_Data = mainData;
            a_SourcePath = mainPath;
            return SaveSlotState.UnsupportedVersion;
        }

        SaveSlotState backupState = ReadSaveFileState(backupPath, a_SlotIndex, out AccountSaveData backupData);
        SaveSlotState temporaryState = ReadSaveFileState(temporaryPath, a_SlotIndex, out AccountSaveData temporaryData);

        AccountSaveData fallbackData = null;
        string fallbackPath = string.Empty;

        if (backupState == SaveSlotState.Valid)
        {
            fallbackData = backupData;
            fallbackPath = backupPath;
        }

        if (temporaryState == SaveSlotState.Valid &&
            (fallbackData == null || temporaryData.LastSavedUtcTicks >= fallbackData.LastSavedUtcTicks))
        {
            fallbackData = temporaryData;
            fallbackPath = temporaryPath;
        }

        AccountSaveData unsupportedData = null;
        string unsupportedPath = string.Empty;

        if (backupState == SaveSlotState.UnsupportedVersion)
        {
            unsupportedData = backupData;
            unsupportedPath = backupPath;
        }

        if (temporaryState == SaveSlotState.UnsupportedVersion &&
            (unsupportedData == null || temporaryData.LastSavedUtcTicks >= unsupportedData.LastSavedUtcTicks))
        {
            unsupportedData = temporaryData;
            unsupportedPath = temporaryPath;
        }

        if (fallbackData != null)
        {
            if (unsupportedData != null && unsupportedData.LastSavedUtcTicks > fallbackData.LastSavedUtcTicks)
            {
                a_Data = unsupportedData;
                a_SourcePath = unsupportedPath;
                return SaveSlotState.UnsupportedVersion;
            }

            a_Data = fallbackData;
            a_SourcePath = fallbackPath;
            return SaveSlotState.Valid;
        }

        if (unsupportedData != null)
        {
            a_Data = unsupportedData;
            a_SourcePath = unsupportedPath;
            return SaveSlotState.UnsupportedVersion;
        }

        bool hasAnyFile = mainState != SaveSlotState.Empty ||
                          backupState != SaveSlotState.Empty ||
                          temporaryState != SaveSlotState.Empty;

        return hasAnyFile ? SaveSlotState.Corrupted : SaveSlotState.Empty;
    } //private SaveSlotState ResolveSlotData()

    //지정 파일을 읽고 Save Data의 구조 및 Version 유효성을 검사
    private SaveSlotState ReadSaveFileState(string a_FilePath, int a_ExpectedSlotIndex, out AccountSaveData a_Data)
    {
        a_Data = null;

        if (File.Exists(a_FilePath) == false)
            return SaveSlotState.Empty;

        try
        {
            string json = File.ReadAllText(a_FilePath, s_Utf8NoBom);

            if (string.IsNullOrWhiteSpace(json))
                return SaveSlotState.Corrupted;

            a_Data = JsonUtility.FromJson<AccountSaveData>(json);

            return ValidateSaveData(a_Data, a_ExpectedSlotIndex);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Save 파일을 읽지 못했습니다. Path = {a_FilePath}, Error = {exception.Message}", this);
            return SaveSlotState.Corrupted;
        }
    } //private SaveSlotState ReadSaveFileState()

    //Save Data의 필수 Metadata와 Character 데이터 유효성을 검사
    private SaveSlotState ValidateSaveData(AccountSaveData a_Data, int a_ExpectedSlotIndex)
    {
        if (a_Data == null)
            return SaveSlotState.Corrupted;

        if (a_Data.Version <= 0)
            return SaveSlotState.Corrupted;

        if (SaveVersion.IsSupported(a_Data.Version) == false)
            return SaveSlotState.UnsupportedVersion;

        if (a_Data.SlotIndex != a_ExpectedSlotIndex)
            return SaveSlotState.Corrupted;

        if (string.IsNullOrWhiteSpace(a_Data.SaveId) || string.IsNullOrWhiteSpace(a_Data.PlayerName))
            return SaveSlotState.Corrupted;

        if (a_Data.CreatedUtcTicks <= 0 || a_Data.LastSavedUtcTicks < a_Data.CreatedUtcTicks)
            return SaveSlotState.Corrupted;

        if (a_Data.TotalPlayTimeSeconds < 0 || a_Data.Gold < 0)
            return SaveSlotState.Corrupted;

        if (IsCharicTypeValid(a_Data.LastSelectedCharicType) == false)
            return SaveSlotState.Corrupted;

        if (a_Data.Characters == null || a_Data.Characters.Count <= 0)
            return SaveSlotState.Corrupted;

        HashSet<CharicType> registeredCharacters = new HashSet<CharicType>();
        bool hasLastSelectedCharacter = false;

        for (int i = 0; i < a_Data.Characters.Count; i++)
        {
            CharacterSaveData characterData = a_Data.Characters[i];

            if (characterData == null || IsCharicTypeValid(characterData.CharicType) == false)
                return SaveSlotState.Corrupted;

            if (characterData.EquippedMajorSkillKeys == null)
                return SaveSlotState.Corrupted;

            if (registeredCharacters.Add(characterData.CharicType) == false)
                return SaveSlotState.Corrupted;

            if (characterData.CharicType == a_Data.LastSelectedCharicType)
                hasLastSelectedCharacter = true;
        }

        return hasLastSelectedCharacter ? SaveSlotState.Valid : SaveSlotState.Corrupted;
    } //private SaveSlotState ValidateSaveData()

    //Save Data를 Temporary 파일에 먼저 기록한 뒤 Main 파일과 Backup 파일을 안전하게 갱신
    private bool TryWriteSaveFile(AccountSaveData a_Data)
    {
        if (a_Data == null || IsSlotIndexValid(a_Data.SlotIndex) == false)
            return false;

        if (ValidateSaveData(a_Data, a_Data.SlotIndex) != SaveSlotState.Valid)
        {
            Debug.LogError("유효하지 않은 AccountSaveData는 저장할 수 없습니다.", this);
            return false;
        }

        if (EnsureSaveDirectory() == false)
            return false;

        string mainPath = GetMainSavePath(a_Data.SlotIndex);
        string backupPath = GetBackupSavePath(a_Data.SlotIndex);
        string temporaryPath = GetTemporarySavePath(a_Data.SlotIndex);

        try
        {
            string json = JsonUtility.ToJson(a_Data, true);
            File.WriteAllText(temporaryPath, json, s_Utf8NoBom);

            SaveSlotState temporaryState = ReadSaveFileState(temporaryPath, a_Data.SlotIndex, out _);

            if (temporaryState != SaveSlotState.Valid)
            {
                Debug.LogError($"Temporary Save 검증에 실패했습니다. Slot = {a_Data.SlotIndex}", this);
                return false;
            }

            if (File.Exists(mainPath))
            {
                SaveSlotState currentMainState = ReadSaveFileState(mainPath, a_Data.SlotIndex, out _);

                if (currentMainState == SaveSlotState.Valid)
                    File.Copy(mainPath, backupPath, true);
            }

            File.Copy(temporaryPath, mainPath, true);
            File.Delete(temporaryPath);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save Slot {a_Data.SlotIndex} 저장에 실패했습니다: {exception.Message}", this);
            return false;
        }
    } //private bool TryWriteSaveFile()

    //현재 Active Save의 마지막 선택 Character를 변경하고 Character 저장 데이터 존재를 보장
    public bool SetLastSelectedCharacter(CharicType a_CharicType)
    {
        if (HasActiveSave == false)
        {
            Debug.LogWarning("마지막 Character를 변경할 Active Save가 없습니다.", this);
            return false;
        }

        if (IsCharicTypeValid(a_CharicType) == false)
        {
            Debug.LogWarning($"유효하지 않은 Character Type입니다: {a_CharicType}", this);
            return false;
        }

        if (GetOrCreateCharacterSaveData(a_CharicType) == null)
            return false;

        if (m_ActiveSaveData.LastSelectedCharicType == a_CharicType)
            return true;

        m_ActiveSaveData.LastSelectedCharicType = a_CharicType;

        MarkDirty(SaveDirtyFlags.CharacterSelection);

        return true;
    } //public bool SetLastSelectedCharacter()

    //Backup 또는 Temporary Save가 정상일 경우 Main Save 파일로 복원
    private bool TryRestoreMainFile(int a_SlotIndex, string a_SourcePath)
    {
        if (string.IsNullOrWhiteSpace(a_SourcePath) || File.Exists(a_SourcePath) == false)
            return false;

        string mainPath = GetMainSavePath(a_SlotIndex);

        try
        {
            File.Copy(a_SourcePath, mainPath, true);

            string temporaryPath = GetTemporarySavePath(a_SlotIndex);

            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Save Slot {a_SlotIndex}의 Main 파일 복원에 실패했습니다: {exception.Message}", this);
            return false;
        }
    } //private bool TryRestoreMainFile()

    //지정 Save 파일이 존재하면 삭제
    private bool TryDeleteFile(string a_FilePath)
    {
        try
        {
            if (File.Exists(a_FilePath))
                File.Delete(a_FilePath);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save 파일 삭제에 실패했습니다. Path = {a_FilePath}, Error = {exception.Message}", this);
            return false;
        }
    } //private bool TryDeleteFile()

    //현재 활성 Save의 플레이 시간 측정을 시작
    private void StartPlayTimeTracking()
    {
        m_LastPlayTimeCheckpoint = Time.realtimeSinceStartupAsDouble;
        m_PlayTimeRemainderSeconds = 0d;
    } //private void StartPlayTimeTracking()

    //마지막 Checkpoint 이후 경과 시간을 Active Save의 총 플레이 시간에 반영
    private void UpdateTrackedPlayTime()
    {
        if (HasActiveSave == false)
            return;

        double currentTime = Time.realtimeSinceStartupAsDouble;
        double elapsedTime = Math.Max(0d, currentTime - m_LastPlayTimeCheckpoint);

        m_LastPlayTimeCheckpoint = currentTime;
        m_PlayTimeRemainderSeconds += elapsedTime;

        int wholeSeconds = (int)Math.Floor(m_PlayTimeRemainderSeconds);

        if (wholeSeconds <= 0)
            return;

        long totalPlayTime = (long)m_ActiveSaveData.TotalPlayTimeSeconds + wholeSeconds;

        m_ActiveSaveData.TotalPlayTimeSeconds = (int)Math.Min(totalPlayTime, int.MaxValue);
        m_PlayTimeRemainderSeconds -= wholeSeconds;
    } //private void UpdateTrackedPlayTime()

    //Runtime 플레이 시간 추적 값 초기화
    private void ResetPlayTimeTracking()
    {
        m_LastPlayTimeCheckpoint = 0d;
        m_PlayTimeRemainderSeconds = 0d;
    } //private void ResetPlayTimeTracking()

    //현재 Player Runtime 상태와 Save Data를 비교하고 실제 변경된 영구 데이터만 반영
    public bool CaptureRuntimeState(Player a_Player)
    {
        if (HasActiveSave == false)
        {
            Debug.LogWarning("Runtime 상태를 저장할 Active Save가 없습니다.", this);
            return false;
        }

        if (a_Player == null)
        {
            Debug.LogWarning("Runtime 상태를 저장할 Player가 없습니다.", this);
            return false;
        }

        CurrencySession currencySession = CurrencySession.Instance;

        if (currencySession != null && m_ActiveSaveData.Gold != currencySession.Gold)
        {
            m_ActiveSaveData.Gold = currencySession.Gold;
            MarkDirty(SaveDirtyFlags.Gold);
        }

        Charic charicData = a_Player.GetCharicData();

        if (charicData == null || IsCharicTypeValid(charicData.m_ChrType) == false)
        {
            Debug.LogWarning("Player의 Character Data가 없어 Character 상태를 저장하지 못했습니다.", this);
            return false;
        }

        if (m_ActiveSaveData.LastSelectedCharicType != charicData.m_ChrType)
        {
            m_ActiveSaveData.LastSelectedCharicType = charicData.m_ChrType;
            MarkDirty(SaveDirtyFlags.CharacterSelection);
        }

        if (charicData is ICharicSkillLoadoutProvider loadoutProvider &&
            SyncSkillLoadoutToSave(charicData, loadoutProvider) == false)
        {
            return false;
        }

        return true;
    } //public bool CaptureRuntimeState()

    //Active Account Save Data를 현재 Player Runtime에 적용
    public bool ApplyActiveSaveToRuntime(Player a_Player)
    {
        if (HasActiveSave == false)
        {
            Debug.LogWarning("Runtime에 적용할 Active Save가 없습니다.", this);
            return false;
        }

        if (a_Player == null)
        {
            Debug.LogWarning("Save Data를 적용할 Player가 없습니다.", this);
            return false;
        }

        m_IsApplyingSaveData = true;

        try
        {
            CurrencySession currencySession = CurrencySession.Instance;

            if (currencySession != null)
                currencySession.LoadFromSave(m_ActiveSaveData.Gold);

            Charic charicData = a_Player.GetCharicData();

            if (charicData == null)
            {
                Debug.LogWarning("Save Data를 적용할 Character Data가 없습니다.", this);
                return false;
            }

            CharacterSaveData characterSaveData = FindCharacterSaveData(charicData.m_ChrType);

            if (characterSaveData == null)
                return true;

            if (charicData is SkillLoadoutCharic skillLoadoutCharic &&
                characterSaveData.EquippedMajorSkillKeys.Count > 0)
            {
                return skillLoadoutCharic.ApplyEquippedMajorSkillKeys(characterSaveData.EquippedMajorSkillKeys);
            }

            return true;
        }
        finally
        {
            m_IsApplyingSaveData = false;
        }
    } //public bool ApplyActiveSaveToRuntime()

    //Dirty 상태 또는 강제 플레이 시간 저장 조건이 있을 때만 Active Slot 저장
    public bool SaveIfDirty(bool a_ForcePlayTimeSave = false)
    {
        if (HasActiveSave == false)
            return false;

        bool hasPendingPlayTime = a_ForcePlayTimeSave && HasPendingPlayTime();

        if (IsDirty == false && hasPendingPlayTime == false)
            return true;

        return SaveActiveSlot();
    } //public bool SaveIfDirty()

    //현재 파일에 반영되지 않은 1초 이상의 플레이 시간이 존재하는지 반환
    private bool HasPendingPlayTime()
    {
        if (HasActiveSave == false)
            return false;

        double currentTime = Time.realtimeSinceStartupAsDouble;
        double elapsedTime = Math.Max(0d, currentTime - m_LastPlayTimeCheckpoint);

        return m_PlayTimeRemainderSeconds + elapsedTime >= 1d;
    } //private bool HasPendingPlayTime()

    //현재 Player Runtime 데이터를 동기화하고 실제 변경 사항이 있을 때만 저장
    public bool SaveRuntimeState(Player a_Player, bool a_ForcePlayTimeSave = false)
    {
        if (HasActiveSave == false)
            return false;

        if (a_Player != null && CaptureRuntimeState(a_Player) == false)
        {
            NotifySaveOperationState(SaveOperationState.Failed);
            return false;
        }

        return SaveIfDirty(a_ForcePlayTimeSave);
    } //public bool SaveRuntimeState()

    //지정 Character의 저장 데이터를 반환하고 없으면 새로 생성
    private CharacterSaveData GetOrCreateCharacterSaveData(CharicType a_CharicType)
    {
        CharacterSaveData characterSaveData = FindCharacterSaveData(a_CharicType);

        if (characterSaveData != null)
            return characterSaveData;

        if (m_ActiveSaveData == null || IsCharicTypeValid(a_CharicType) == false)
            return null;

        characterSaveData = new CharacterSaveData
        {
            CharicType = a_CharicType
        };

        m_ActiveSaveData.MutableCharacters.Add(characterSaveData);

        MarkDirty(SaveDirtyFlags.CharacterSelection);

        return characterSaveData;
    } //private CharacterSaveData GetOrCreateCharacterSaveData()

    //Application이 Background로 이동할 때 현재 Runtime Save 시도
    private void OnApplicationPause(bool a_IsPaused)
    {
        if (a_IsPaused == false || HasActiveSave == false)
            return;

        TrySaveCurrentRuntimeState();
    }

    //Application 종료 직전 현재 Runtime Save 시도
    private void OnApplicationQuit()
    {
        if (HasActiveSave == false)
            return;

        TrySaveCurrentRuntimeState();
    }

    //현재 Gameplay Player와 플레이 시간을 포함하여 필요한 경우에만 Save 시도
    private void TrySaveCurrentRuntimeState()
    {
        Player player = FindAnyObjectByType<Player>();

        if (player != null)
        {
            SaveRuntimeState(player, true);
            return;
        }

        SaveIfDirty(true);
    } //private void TrySaveCurrentRuntimeState()

    //현재 Active Save에서 지정 Character의 저장 데이터 탐색
    private CharacterSaveData FindCharacterSaveData(CharicType a_CharicType)
    {
        if (m_ActiveSaveData == null || m_ActiveSaveData.Characters == null)
            return null;

        for (int i = 0; i < m_ActiveSaveData.Characters.Count; i++)
        {
            CharacterSaveData characterSaveData = m_ActiveSaveData.Characters[i];

            if (characterSaveData != null && characterSaveData.CharicType == a_CharicType)
                return characterSaveData;
        }

        return null;
    }

    //Save Directory가 존재하도록 생성
    private bool EnsureSaveDirectory()
    {
        string directoryPath = GetSaveDirectoryPath();

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            Debug.LogError("Save Directory 경로를 생성할 수 없습니다.", this);
            return false;
        }

        try
        {
            Directory.CreateDirectory(directoryPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save Directory 생성에 실패했습니다: {exception.Message}", this);
            return false;
        }
    } //private bool EnsureSaveDirectory()

    //지정 Slot에 Main, Backup 또는 Temporary 파일이 하나라도 존재하는지 반환
    private bool HasAnySlotFile(int a_SlotIndex)
    {
        return File.Exists(GetMainSavePath(a_SlotIndex)) ||
               File.Exists(GetBackupSavePath(a_SlotIndex)) ||
               File.Exists(GetTemporarySavePath(a_SlotIndex));
    } //private bool HasAnySlotFile()

    //지정 Save Slot Index가 지원 범위에 포함되는지 반환
    private bool IsSlotIndexValid(int a_SlotIndex)
    {
        return a_SlotIndex >= 0 && a_SlotIndex < SlotCount;
    } //private bool IsSlotIndexValid()

    //지정 Character Type이 실제 Character 범위에 포함되는지 반환
    private bool IsCharicTypeValid(CharicType a_CharicType)
    {
        int charicType = (int)a_CharicType;
        return charicType >= 0 && charicType < (int)CharicType.Count;
    } //private bool IsCharicTypeValid()

    //Save Data Root Directory 경로 반환
    private string GetSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, SaveDirectoryName);
    } //private string GetSaveDirectoryPath()

    //지정 Slot의 Main Save 파일 경로 반환
    private string GetMainSavePath(int a_SlotIndex)
    {
        return GetSlotSavePath(a_SlotIndex, MainSaveExtension);
    } //private string GetMainSavePath()

    //지정 Slot의 Backup Save 파일 경로 반환
    private string GetBackupSavePath(int a_SlotIndex)
    {
        return GetSlotSavePath(a_SlotIndex, BackupSaveExtension);
    } //private string GetBackupSavePath()

    //지정 Slot의 Temporary Save 파일 경로 반환
    private string GetTemporarySavePath(int a_SlotIndex)
    {
        return GetSlotSavePath(a_SlotIndex, TemporarySaveExtension);
    } //private string GetTemporarySavePath()

    //지정 Slot과 확장자를 기준으로 Save 파일 경로 생성
    private string GetSlotSavePath(int a_SlotIndex, string a_Extension)
    {
        return Path.Combine(GetSaveDirectoryPath(), $"{SaveFilePrefix}{a_SlotIndex}{a_Extension}");
    } //private string GetSlotSavePath()

    //현재 Gameplay Player의 영구 데이터 변경 Event를 SaveDataManager에 연결
    public void BindRuntime(Player a_Player)
    {
        UnbindRuntime();

        if (HasActiveSave == false || a_Player == null)
            return;

        m_BoundPlayer = a_Player;

        m_BoundCurrencySession = CurrencySession.Instance;

        if (m_BoundCurrencySession != null)
        {
            m_BoundCurrencySession.OnGoldChanged -= OnGoldChanged;
            m_BoundCurrencySession.OnGoldChanged += OnGoldChanged;
        }

        m_BoundPlayer.OnCharicChanged -= OnBoundPlayerCharicChanged;
        m_BoundPlayer.OnCharicChanged += OnBoundPlayerCharicChanged;

        BindSkillLoadoutProvider(m_BoundPlayer.GetCharicData());
    } //public void BindRuntime()

    //현재 Runtime 영구 데이터 변경 Event 연결 해제
    public void UnbindRuntime()
    {
        if (m_BoundCurrencySession != null)
            m_BoundCurrencySession.OnGoldChanged -= OnGoldChanged;

        if (m_BoundPlayer != null)
            m_BoundPlayer.OnCharicChanged -= OnBoundPlayerCharicChanged;

        if (m_BoundSkillLoadoutProvider != null)
            m_BoundSkillLoadoutProvider.OnSkillLoadoutChanged -= OnSkillLoadoutChanged;

        m_BoundCurrencySession = null;
        m_BoundPlayer = null;
        m_BoundSkillLoadoutProvider = null;
    } //public void UnbindRuntime()

    //Player Character 변경 시 현재 Character의 Skill Loadout Event를 다시 연결
    private void OnBoundPlayerCharicChanged(Charic a_CharicData)
    {
        BindSkillLoadoutProvider(a_CharicData);
    } //private void OnBoundPlayerCharicChanged()

    //현재 Character의 Skill Loadout 변경 Event 연결
    private void BindSkillLoadoutProvider(Charic a_CharicData)
    {
        if (m_BoundSkillLoadoutProvider != null)
            m_BoundSkillLoadoutProvider.OnSkillLoadoutChanged -= OnSkillLoadoutChanged;

        m_BoundSkillLoadoutProvider = a_CharicData as ICharicSkillLoadoutProvider;

        if (m_BoundSkillLoadoutProvider == null)
            return;

        m_BoundSkillLoadoutProvider.OnSkillLoadoutChanged -= OnSkillLoadoutChanged;
        m_BoundSkillLoadoutProvider.OnSkillLoadoutChanged += OnSkillLoadoutChanged;
    } //private void BindSkillLoadoutProvider()

    //Runtime Gold 변경을 Active Save Data에 반영하고 Dirty 상태 설정
    private void OnGoldChanged(int a_Gold)
    {
        if (m_IsApplyingSaveData || HasActiveSave == false)
            return;

        int gold = Mathf.Max(0, a_Gold);

        if (m_ActiveSaveData.Gold == gold)
            return;

        m_ActiveSaveData.Gold = gold;
        MarkDirty(SaveDirtyFlags.Gold);
    } //private void OnGoldChanged()

    //현재 Runtime Skill Loadout 변경을 해당 Character Save Data에 반영
    private void OnSkillLoadoutChanged()
    {
        if (m_IsApplyingSaveData ||
            HasActiveSave == false ||
            m_BoundPlayer == null ||
            m_BoundSkillLoadoutProvider == null)
        {
            return;
        }

        Charic charicData = m_BoundPlayer.GetCharicData();

        if (charicData == null)
            return;

        SyncSkillLoadoutToSave(charicData, m_BoundSkillLoadoutProvider);
    } //private void OnSkillLoadoutChanged()

    //현재 Runtime Skill Loadout과 저장 데이터를 비교하고 실제 변경 시 Save Data 갱신
    private bool SyncSkillLoadoutToSave(Charic a_CharicData, ICharicSkillLoadoutProvider a_LoadoutProvider)
    {
        if (a_CharicData == null ||
            a_LoadoutProvider == null ||
            IsCharicTypeValid(a_CharicData.m_ChrType) == false)
        {
            return false;
        }

        CharacterSaveData characterSaveData = GetOrCreateCharacterSaveData(a_CharicData.m_ChrType);

        if (characterSaveData == null)
            return false;

        int slotCount = Mathf.Max(0, a_LoadoutProvider.GetSkillSlotCount());
        bool hasChanged = characterSaveData.EquippedMajorSkillKeys.Count != slotCount;

        if (hasChanged == false)
        {
            for (int i = 0; i < slotCount; i++)
            {
                CharicSkillUIInfo skillInfo = a_LoadoutProvider.GetEquippedSkillInfo(i);
                string runtimeSkillKey = skillInfo.m_SkillKey ?? string.Empty;
                string savedSkillKey = characterSaveData.EquippedMajorSkillKeys[i] ?? string.Empty;

                if (runtimeSkillKey == savedSkillKey)
                    continue;

                hasChanged = true;
                break;
            }
        }

        if (hasChanged == false)
            return true;

        List<string> skillKeys = characterSaveData.MutableEquippedMajorSkillKeys;
        skillKeys.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            CharicSkillUIInfo skillInfo = a_LoadoutProvider.GetEquippedSkillInfo(i);
            skillKeys.Add(skillInfo.m_SkillKey ?? string.Empty);
        }

        MarkDirty(SaveDirtyFlags.SkillLoadout);
        return true;
    } //private bool SyncSkillLoadoutToSave()

    //지정 Save 변경 상태를 현재 Dirty Flag에 추가
    private void MarkDirty(SaveDirtyFlags a_DirtyFlags)
    {
        if (a_DirtyFlags == SaveDirtyFlags.None)
            return;

        m_DirtyFlags |= a_DirtyFlags;
    } //private void MarkDirty()

    //현재 Dirty 상태 초기화
    private void ResetDirtyState()
    {
        m_DirtyFlags = SaveDirtyFlags.None;
    } //private void ResetDirtyState()
} //public class SaveDataManager