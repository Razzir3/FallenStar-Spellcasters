using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class LobbyControlUI : MonoBehaviour
{
    #region Variable
    public static LobbyControlUI Instance;

    private MultiplayerManager m_multiplayerInstance;

    public bool m_debugMode = false;
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        Instance = this;
    }
    void Update()
    {
        if (m_multiplayerInstance == null)
        {
            var _searchResult = FindObjectsByType<MultiplayerManager>(FindObjectsSortMode.None);
            if (_searchResult != null)
            {
                if (_searchResult.Length > 1)
                {
                    Debug.LogError("Multiplayer Instance multiplied!");
                }
                else
                {
                    m_multiplayerInstance = _searchResult[0];
                }
            }
        }
    }

    #endregion

    #region ButtonAction
    public async void OnClickCreateLobby()
    {
        await m_multiplayerInstance.StartHost();
        StartCoroutine(DelayCheck());
    }
    //todo
    public async void OnClickFindAndJoinLobby()
    {
        await m_multiplayerInstance.FindAndJoinLobby();
    }
    private void SetUnactiveFlag(bool unactive = false)
    {
        gameObject.SetActive(unactive);
    }

    //todo Need rework?
    public void CheckLobbyAndChangeUI()
    {
        StartCoroutine(DelayCheck());
    }

    private IEnumerator DelayCheck()
    {
        float _duration = 10f;
        float _interval = 5f;

        float _timer = 0f;

        while (_timer < _duration)
        {
            bool __result = m_multiplayerInstance.LobbyConnected();

            if (__result)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    SpawnPanelUI.Instance.gameObject.SetActive(true);
                }

                SetUnactiveFlag();
                if (m_debugMode)
                {
                    Debug.Log("Lobby exist, continue");
                }
                yield break;
            }

            yield return new WaitForSeconds(_interval);

            _timer += _interval;
        }
        if (m_debugMode)
        {
            Debug.Log("Cant find lobby by UI");
        }
    }
    #endregion
}
