using UnityEngine;

public class SpawnPanelUI : MonoBehaviour
{
    public static SpawnPanelUI Instance;

    private SessionManager m_sessionManager;

    public MultiplayerManager m_multiplayerInstance;

    private GameMaster m_gameMaster;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (m_sessionManager == null)
        {
            var _searchResult = FindObjectsByType<SessionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                else if(_searchResult.Length == 1)
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
    public void OnClickSpawnPlayers()
    {
        m_gameMaster.SpawnPlayers();
    }

    private void SetUnactiveFlag(bool unactive = false)
    {
        gameObject.SetActive(unactive);
    }
}
