using UnityEngine;
public class DisconnectMenuUI : MonoBehaviour
{
    #region Variable
    public static DisconnectMenuUI Instance;

    private SessionManager m_sessionManager;

    private MultiplayerManager m_multiplayerInstance;

    private GameMaster m_gameMaster;
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }
    void Update()
    {
        if (m_sessionManager == null)
        {
            var _searchResult = FindObjectsByType<SessionManager>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if (_searchResult != null)
            {
                if (_searchResult.Length > 1)
                {
                    Debug.LogError("Session Manager Instance multiplied!");
                }
                else if (_searchResult.Length == 1)
                {
                    m_sessionManager = _searchResult[0];
                }
            }
        }
        if (m_gameMaster == null)
        {
            var _searchResult = FindObjectsByType<GameMaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (_searchResult != null)
            {
                if (_searchResult.Length > 1)
                {
                    Debug.LogError("Game Master Instance multiplied!");
                }
                else if (_searchResult.Length == 1)
                {
                    m_gameMaster = _searchResult[0];
                }
            }
        }
        if (m_multiplayerInstance == null)
        {
            var _searchResult = FindObjectsByType<MultiplayerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (_searchResult != null)
            {
                if (_searchResult.Length > 1)
                {
                    Debug.LogError("Multiplayer Instance multiplied!");
                }
                else if (_searchResult.Length == 1)
                {
                    m_multiplayerInstance = _searchResult[0];
                }
            }
        }
    }
    #endregion

    #region ButtonActionFunc
    public void OnClickLobbyDisconnect()
    {
        m_multiplayerInstance.Disconnect();

        LobbyControlUI.Instance.gameObject.SetActive(true);
        SpawnPanelUI.Instance.gameObject.SetActive(false);

        SetUnactiveFlag();
    }

    private void SetUnactiveFlag(bool unactive = false)
    {
        gameObject.SetActive(unactive);
    }
    #endregion
}
