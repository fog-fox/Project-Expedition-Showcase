using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class NetworkSessionManager : MonoBehaviour
{
    /*
    Photon Fusion NetworkRunner의 생성과 수명주기를 관리하고
    Host 및 Client Session의 시작, 종료와 기본 Network 설정을 담당
    */
    [Header("Player")]
    [SerializeField] private NetworkObject m_PlayerPrefab;

    private const string DefaultSessionName = "DevelopmentRoom";
    private const int MinimumPlayers = 1;
    private const int MaximumPlayers = 4;

    public static NetworkSessionManager Instance
    {
        get;
        private set;
    }

    [Header("Session")]
    [SerializeField] private string m_DefaultSessionName = DefaultSessionName;
    [SerializeField, Range(MinimumPlayers, MaximumPlayers)] private int m_MaxPlayers = MaximumPlayers;

    private NetworkRunner m_Runner;
    private bool m_IsOperationRunning;

    public NetworkRunner Runner => m_Runner;

    public bool IsRunning =>
        m_Runner != null &&
        m_Runner.IsRunning;

    public bool IsHost =>
        IsRunning &&
        m_Runner.IsServer;

    public bool IsOperationRunning => m_IsOperationRunning;

    //Singleton을 구성하고 Scene 전환에서도 Network Manager 유지
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    } //private void Awake()

    //Inspector에서 Session 설정값 보정
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_DefaultSessionName))
            m_DefaultSessionName = DefaultSessionName;

        m_MaxPlayers = Mathf.Clamp(m_MaxPlayers, MinimumPlayers, MaximumPlayers);
    } //private void OnValidate()

    //지정 Session Name으로 Host Session 시작
    public async Task<bool> StartHostAsync(string a_SessionName = null)
    {
        return await StartSessionAsync(
            GameMode.Host,
            a_SessionName
        );
    } //public async Task<bool> StartHostAsync()

    //지정 Session Name으로 Client Session 참가
    public async Task<bool> StartClientAsync(string a_SessionName = null)
    {
        return await StartSessionAsync(
            GameMode.Client,
            a_SessionName
        );
    } //public async Task<bool> StartClientAsync()

    //현재 Network Session을 정상 종료
    public async Task ShutdownAsync()
    {
        if (m_IsOperationRunning)
        {
            Debug.LogWarning(
                $"[{name}] 다른 Network 작업이 진행 중이므로 Shutdown을 시작할 수 없습니다.",
                this
            );

            return;
        }

        if (m_Runner == null)
            return;

        m_IsOperationRunning = true;

        NetworkRunner runner = m_Runner;

        try
        {
            if (runner.IsRunning)
            {
                /*
                Fusion Shutdown은 기본적으로 Runner GameObject도 제거합니다.
                명시적으로 true를 전달하여 현재 수명주기 정책을 분명히 합니다.
                */
                await runner.Shutdown(destroyGameObject: true);
            }
            else
            {
                DestroyRunnerObject(runner);
            }

            Debug.Log(
                $"[{name}] Network Session 종료 완료.",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            DestroyRunnerObject(runner);
        }
        finally
        {
            if (m_Runner == runner)
                m_Runner = null;

            m_IsOperationRunning = false;
        }
    } //public async Task ShutdownAsync()

    //Host 또는 Client Mode로 하나의 Network Session 시작
    private async Task<bool> StartSessionAsync(
        GameMode a_GameMode,
        string a_SessionName)
    {
        if (m_PlayerPrefab == null)
        {
            Debug.LogError(
                $"[{name}] Network Player Prefab이 설정되지 않았습니다.",
                this
            );

            return false;
        }
        if (m_IsOperationRunning)
        {
            Debug.LogWarning(
                $"[{name}] 다른 Network 작업이 이미 진행 중입니다.",
                this
            );

            return false;
        }

        if (IsRunning)
        {
            Debug.LogWarning(
                $"[{name}] NetworkRunner가 이미 실행 중입니다.",
                this
            );

            return false;
        }

        m_IsOperationRunning = true;

        try
        {
            ClearStaleRunner();

            if (TryCreateActiveSceneInfo(out NetworkSceneInfo sceneInfo) == false)
                return false;

            string sessionName = GetSessionName(a_SessionName);

            NetworkRunner runner = CreateRunner(
                out NetworkSceneManagerDefault sceneManager
            );

            StartGameArgs startGameArgs = new StartGameArgs
            {
                GameMode = a_GameMode,
                SessionName = sessionName,
                PlayerCount = Mathf.Clamp(m_MaxPlayers, MinimumPlayers, MaximumPlayers),
                Scene = sceneInfo,
                SceneManager = sceneManager
            };

            StartGameResult result = await runner.StartGame(startGameArgs);

            if (result.Ok == false)
            {
                Debug.LogError(
                    $"[{name}] Network Session 시작 실패. " +
                    $"Mode={a_GameMode}, Session={sessionName}, " +
                    $"Reason={result.ShutdownReason}",
                    this
                );

                DestroyRunnerObject(runner);
                return false;
            }

            Debug.Log(
                $"[{name}] Network Session 시작 완료. " +
                $"Mode={a_GameMode}, Session={sessionName}, MaxPlayers={m_MaxPlayers}",
                this
            );

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            if (m_Runner != null && m_Runner.IsRunning == false)
                DestroyRunnerObject(m_Runner);

            return false;
        }
        finally
        {
            m_IsOperationRunning = false;
        }
    } //private async Task<bool> StartSessionAsync()

    //현재 Active Scene을 Fusion Network Scene 정보로 변환
    private bool TryCreateActiveSceneInfo(out NetworkSceneInfo a_SceneInfo)
    {
        a_SceneInfo = new NetworkSceneInfo();

        Scene activeScene = SceneManager.GetActiveScene();
        int buildIndex = activeScene.buildIndex;

        if (buildIndex < 0 ||
            buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError(
                $"[{name}] Active Scene '{activeScene.name}'이 Build Settings에 등록되어 있지 않습니다. " +
                "Network Session 시작 전에 Scene을 Build Settings에 등록해야 합니다.",
                this
            );

            return false;
        }

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);

        if (sceneRef.IsValid == false)
        {
            Debug.LogError(
                $"[{name}] Active Scene을 유효한 Fusion SceneRef로 변환할 수 없습니다. " +
                $"Scene={activeScene.name}, BuildIndex={buildIndex}",
                this
            );

            return false;
        }

        a_SceneInfo.AddSceneRef(
            sceneRef,
            LoadSceneMode.Single
        );

        return true;
    } //private bool TryCreateActiveSceneInfo()

    //새 Fusion NetworkRunner와 기본 Network Component 생성
    private NetworkRunner CreateRunner(out NetworkSceneManagerDefault a_SceneManager)
    {
        GameObject runnerObject = new GameObject("NetworkRunner");
        runnerObject.transform.SetParent(transform);

        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();

        runner.ProvideInput = true;

        a_SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        NetworkPlayerSpawner playerSpawner = runnerObject.AddComponent<NetworkPlayerSpawner>();
        playerSpawner.Initialize(m_PlayerPrefab);

        m_Runner = runner;

        return runner;
    } //private NetworkRunner CreateRunner()

    //종료되었거나 실패 후 남아 있는 이전 Runner 제거
    private void ClearStaleRunner()
    {
        if (m_Runner == null)
            return;

        if (m_Runner.IsRunning)
            return;

        DestroyRunnerObject(m_Runner);
    } //private void ClearStaleRunner()

    //재사용할 수 없는 Runner GameObject를 비활성화하고 제거
    private void DestroyRunnerObject(NetworkRunner a_Runner)
    {
        if (a_Runner == null)
            return;

        if (m_Runner == a_Runner)
            m_Runner = null;

        GameObject runnerObject = a_Runner.gameObject;

        if (runnerObject == null)
            return;

        runnerObject.SetActive(false);
        Destroy(runnerObject);
    } //private void DestroyRunnerObject()

    //입력값 또는 기본 설정에서 실제 사용할 Session Name 반환
    private string GetSessionName(string a_SessionName)
    {
        if (string.IsNullOrWhiteSpace(a_SessionName) == false)
            return a_SessionName.Trim();

        if (string.IsNullOrWhiteSpace(m_DefaultSessionName) == false)
            return m_DefaultSessionName.Trim();

        return DefaultSessionName;
    } //private string GetSessionName()

    //Singleton Reference 정리
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    } //private void OnDestroy()
} //public class NetworkSessionManager