using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class LobbyControlUI : MonoBehaviour
{
    #region Variable
    public static LobbyControlUI Instance;

    private MultiplayerManager m_multiplayerInstance;
    private bool? m_lastConnected;

    [SerializeField]
    private GameObject m_lobbySearchPanel;

    [SerializeField]
    private GameObject m_currentLobbyPanel;

    public bool m_debugMode = false;
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        m_debugMode = DebugModeGuard.Allow(m_debugMode);
        Instance = this;
    }
    private void Start()
    {
        FindMultiplayerManager();
        RefreshUI();
    }
    void Update()
    {
        if (m_multiplayerInstance == null)
        {
            m_multiplayerInstance = MultiplayerManager.Instance;
        }
        RefreshUI();
    }

    #endregion

    #region ButtonAction
    public async void OnClickCreateLobby()
    {
        if(m_multiplayerInstance == null)
        {
            Debug.LogError("LobbyControlUI: No multiplayerManager");
            return;
        }
        await m_multiplayerInstance.StartHost();

        RefreshUI();
    }
    public void OnClickBackToMainMenu()
    {
        if (SceneFlowManager.Instance == null)
            return;

        SceneFlowManager.Instance.LoadMainMenuScene();
    }
    #endregion
    #region PrivateFunc
    private void FindMultiplayerManager()
    {
        if (MultiplayerManager.Instance != null)
        {
            m_multiplayerInstance = MultiplayerManager.Instance;
            return;
        }

        MultiplayerManager[] _searchResult = FindObjectsByType<MultiplayerManager>(FindObjectsSortMode.None);

        if (_searchResult == null || _searchResult.Length == 0)
        {
            return;
        }

        if (_searchResult.Length == 1)
        {
            m_multiplayerInstance = _searchResult[0];
        }
        else
        {
            Debug.LogError("MultiplayerInstance multiplied");
        }
    }
    private void RefreshUI()
    {
        bool _connected = m_multiplayerInstance != null && m_multiplayerInstance.LobbyConnected();
        if (m_lastConnected == _connected)
            return;
        m_lastConnected = _connected;

        if(m_lobbySearchPanel != null)
        {
            m_lobbySearchPanel.SetActive(!_connected);
        }

        if(m_currentLobbyPanel != null)
        {
            m_currentLobbyPanel.SetActive(_connected);
        }
    }
    public void OnClickLeaveLobby()
    {
        if (MultiplayerManager.Instance == null)
        {
            Debug.LogError("OnClickLeaveLobby: MultiplayerManager.Instance is null.");
            return;
        }

        MultiplayerManager.Instance.Disconnect();
    }
    #endregion
}
