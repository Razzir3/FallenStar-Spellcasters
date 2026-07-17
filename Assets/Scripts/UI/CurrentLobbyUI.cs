using UnityEngine;
using System.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using System.Threading.Tasks;

public class CurrentLobbyUI : MonoBehaviour
{
    #region Variable
    [SerializeField]
    private LobbyPlayerListUI m_currentLobbyPlayerListUI;

    [SerializeField]
    private TMP_Text m_lobbyStatusText;

    [SerializeField]
    private GameObject m_startGameButtonObject;

    [SerializeField]
    private float m_refreshInterval = 2f;

    [Header("Localization")]
    [SerializeField]
    private LocalizedString m_notConnectedStatusString =
        new LocalizedString("UI_table", "lobby_status_not_connected");

    [SerializeField]
    private LocalizedString m_readyStatusString =
        new LocalizedString("UI_table", "lobby_status_ready");

    [SerializeField]
    private LocalizedString m_waitingStatusString =
        new LocalizedString("UI_table", "lobby_status_waiting");

    private readonly IntVariable m_currentPlayers = new IntVariable();
    private readonly IntVariable m_maxPlayers = new IntVariable();

    private Coroutine m_refreshCoroutine;

    private int m_refreshVersion;
    private bool m_isClosing;

    private enum LobbyStatusMode
    {
        None,
        NotConnected,
        Ready,
        Waiting
    }

    private LobbyStatusMode m_lobbyStatusMode = LobbyStatusMode.None;
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        AddStatusVariables(m_readyStatusString);
        AddStatusVariables(m_waitingStatusString);
    }

    private void OnEnable()
    {
        m_isClosing = false;
        m_lobbyStatusMode = LobbyStatusMode.None;

        m_refreshVersion++;

        m_notConnectedStatusString.StringChanged += OnNotConnectedStatusChanged;
        m_readyStatusString.StringChanged += OnReadyStatusChanged;
        m_waitingStatusString.StringChanged += OnWaitingStatusChanged;

        m_refreshCoroutine = StartCoroutine(RefreshLobbyLoop(m_refreshVersion));
    }

    private void OnDisable()
    {
        m_isClosing = true;
        m_refreshVersion++;

        if (m_refreshCoroutine != null)
        {
            StopCoroutine(m_refreshCoroutine);
            m_refreshCoroutine = null;
        }

        m_notConnectedStatusString.StringChanged -= OnNotConnectedStatusChanged;
        m_readyStatusString.StringChanged -= OnReadyStatusChanged;
        m_waitingStatusString.StringChanged -= OnWaitingStatusChanged;
    }

    private void OnDestroy()
    {
        m_isClosing = true;
        m_refreshVersion++;
    }
    #endregion

    #region ButtonAction
    public void OnClickStartGame()
    {
        if (MultiplayerManager.Instance == null)
            return;

        MultiplayerManager.Instance.StartGameScene();
    }
    #endregion

    #region PrivateFunc
    private void AddStatusVariables(LocalizedString localizedString)
    {
        if (localizedString == null)
            return;

        if (!localizedString.ContainsKey("currentPlayers"))
            localizedString.Add("currentPlayers", m_currentPlayers);

        if (!localizedString.ContainsKey("maxPlayers"))
            localizedString.Add("maxPlayers", m_maxPlayers);
    }

    private IEnumerator RefreshLobbyLoop(int version)
    {
        while (!m_isClosing && version == m_refreshVersion)
        {
            Task _task = RefreshOnceAsync(version);

            while (!_task.IsCompleted)
            {
                if (this == null)
                    yield break;

                if (m_isClosing || version != m_refreshVersion)
                    yield break;

                yield return null;
            }

            if (_task.IsFaulted)
            {
                Debug.LogException(_task.Exception);
            }

            yield return new WaitForSeconds(m_refreshInterval);
        }
    }

    private async Task RefreshOnceAsync(int version)
    {
        MultiplayerManager _manager = MultiplayerManager.Instance;

        if (_manager == null)
            return;

        await _manager.RefreshCurrentLobby();

        if (this == null)
            return;

        if (m_isClosing || version != m_refreshVersion)
            return;

        if (!isActiveAndEnabled)
            return;

        if (_manager == null)
            return;

        var _lobby = _manager.GetCurrentLobby();

        if (m_currentLobbyPlayerListUI != null)
        {
            m_currentLobbyPlayerListUI.ShowLobby(_lobby);
        }

        bool _isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        bool _canStart = _manager.CanStartTwoPlayerGame();

        if (m_startGameButtonObject != null)
        {
            m_startGameButtonObject.SetActive(_isHost && _canStart);
        }

        if (_lobby == null)
        {
            SetNotConnectedStatus();
            return;
        }

        int _playersCount = _lobby.Players != null ? _lobby.Players.Count : 0;
        int _maxPlayers = _lobby.MaxPlayers;

        if (_canStart)
        {
            SetReadyStatus(_playersCount, _maxPlayers);
        }
        else
        {
            SetWaitingStatus(_playersCount, _maxPlayers);
        }
    }

    private void SetNotConnectedStatus()
    {
        m_lobbyStatusMode = LobbyStatusMode.NotConnected;
        m_notConnectedStatusString.RefreshString();
    }

    private void SetReadyStatus(int currentPlayers, int maxPlayers)
    {
        m_lobbyStatusMode = LobbyStatusMode.Ready;

        m_currentPlayers.Value = currentPlayers;
        m_maxPlayers.Value = maxPlayers;

        m_readyStatusString.RefreshString();
    }

    private void SetWaitingStatus(int currentPlayers, int maxPlayers)
    {
        m_lobbyStatusMode = LobbyStatusMode.Waiting;

        m_currentPlayers.Value = currentPlayers;
        m_maxPlayers.Value = maxPlayers;

        m_waitingStatusString.RefreshString();
    }

    private void OnNotConnectedStatusChanged(string value)
    {
        if (m_lobbyStatusMode != LobbyStatusMode.NotConnected)
            return;

        if (m_lobbyStatusText != null)
        {
            m_lobbyStatusText.text = value;
        }
    }

    private void OnReadyStatusChanged(string value)
    {
        if (m_lobbyStatusMode != LobbyStatusMode.Ready)
            return;

        if (m_lobbyStatusText != null)
        {
            m_lobbyStatusText.text = value;
        }
    }

    private void OnWaitingStatusChanged(string value)
    {
        if (m_lobbyStatusMode != LobbyStatusMode.Waiting)
            return;

        if (m_lobbyStatusText != null)
        {
            m_lobbyStatusText.text = value;
        }
    }
    #endregion
}