using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowManager : MonoBehaviour
{
    #region Variable
    public static SceneFlowManager Instance;

    [Header("Scene Names")]
    [SerializeField] private string m_loginSceneName = "LoginScene";
    [SerializeField] private string m_mainMenuScene = "MainMenuScene";
    [SerializeField] private string m_lobbySceneName = "LobbyScene";
    [SerializeField] private string m_gameSceneName = "GameScene";
    [SerializeField] private string m_settingsSceneName = "SettingsScene";
    [SerializeField] private string m_resultSceneName = "ResultScene";

    private bool m_isLoadingScene = false;

    public string GameSceneName
    {
        get
        {
            return m_gameSceneName;
        }
    }
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        if(Instance !=null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region LoadingFunc
    public void LoadLoginScene()
    {
        LoadOfflineScene(m_loginSceneName);
    }
    public void LoadMainMenuScene()
    {
        LoadOfflineScene(m_mainMenuScene);
    }
    public void LoadLobbyScene()
    {
        LoadOfflineScene(m_lobbySceneName);
    }
    public void LoadSettingsScene()
    {
        LoadOfflineScene(m_settingsSceneName);
    }
    public void LoadResultScene()
    {
        LoadOfflineScene(m_resultSceneName);
    }
    private void LoadOfflineScene(string sceneName)
    {
        if (m_isLoadingScene)
            return;

        m_isLoadingScene = true;

        SceneManager.LoadScene(sceneName);

        m_isLoadingScene = false;
    }
    public void LoadGameSceneNetwork()
    {
        if (m_isLoadingScene)
            return;

        if(NetworkManager.Singleton == null)
        {
            Debug.LogError("LoadSceneNetwork: Singleton is null");
            return;
        }

        if(!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Only server/host can load networkGameScene");
            return;
        }

        if(!NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("NetworkManager is not listening.");
            return;
        }

        m_isLoadingScene = true;

        SceneEventProgressStatus _status = NetworkManager.Singleton.SceneManager.LoadScene(m_gameSceneName, LoadSceneMode.Single);

        if(_status != SceneEventProgressStatus.Started)
        {
            Debug.LogError("Failed to start network scene load. " + _status);
            m_isLoadingScene = false;
        }
    }
    public void MarkNetworkSceneLoadingFinished()
    {
        m_isLoadingScene = false;
    }
    #endregion
}
