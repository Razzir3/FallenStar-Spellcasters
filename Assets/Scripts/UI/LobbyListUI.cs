using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public class LobbyListUI : MonoBehaviour
{
    #region Variable
    [SerializeField]
    private Transform m_lobbyListContent;

    [SerializeField]
    private LobbyListItemUI m_lobbyListItemPrefab;

    [SerializeField]
    private LobbyPlayerListUI m_selectedLobbyPlayerListUI;

    [SerializeField]
    private TMP_Text m_statusText;

    [Header("Localization")]
    [SerializeField]
    private LocalizedString m_lobbyNotFoundString =
        new LocalizedString("UI_table", "lobby_list_not_found");

    [SerializeField]
    private LocalizedString m_foundLobbiesString =
        new LocalizedString("UI_table", "lobby_list_found_count");

    [SerializeField]
    private LocalizedString m_selectedLobbyString =
        new LocalizedString("UI_table", "lobby_list_selected");

    private readonly IntVariable m_lobbiesCount = new IntVariable();
    private readonly StringVariable m_lobbyName = new StringVariable();

    private readonly List<LobbyListItemUI> m_createdItem = new List<LobbyListItemUI>();

    private Lobby m_selectedLobby;

    private enum StatusMode
    {
        None,
        LobbyNotFound,
        FoundLobbies,
        SelectedLobby
    }

    private StatusMode m_statusMode = StatusMode.None;

    private int m_requestVersion;
    private bool m_isClosing;

    public bool m_debugMode = false;
    #endregion

    #region Unity
    private void Awake()
    {
        AddLocalizationVariables();
    }

    private void OnEnable()
    {
        m_isClosing = false;

        m_lobbyNotFoundString.StringChanged += OnLobbyNotFoundStringChanged;
        m_foundLobbiesString.StringChanged += OnFoundLobbiesStringChanged;
        m_selectedLobbyString.StringChanged += OnSelectedLobbyStringChanged;
    }

    private void OnDisable()
    {
        m_isClosing = true;
        m_requestVersion++;

        m_lobbyNotFoundString.StringChanged -= OnLobbyNotFoundStringChanged;
        m_foundLobbiesString.StringChanged -= OnFoundLobbiesStringChanged;
        m_selectedLobbyString.StringChanged -= OnSelectedLobbyStringChanged;
    }

    private void OnDestroy()
    {
        m_isClosing = true;
        m_requestVersion++;
    }
    #endregion

    #region ButtonAction
    public async void OnClickRefreshLobbyList()
    {
        int _version = ++m_requestVersion;

        ClearLobbyList();

        if (MultiplayerManager.Instance == null)
        {
            Debug.LogError("LobbyListUI: No multiplayerManager");
            return;
        }

        List<Lobby> _lobbies = await MultiplayerManager.Instance.FindAvailableLobbies();

        if (this == null)
            return;

        if (m_isClosing || _version != m_requestVersion)
            return;

        if (_lobbies == null || _lobbies.Count == 0)
        {
            SetLobbyNotFoundStatus();

            if (m_selectedLobbyPlayerListUI != null)
            {
                m_selectedLobbyPlayerListUI.ShowLobby(null);
            }

            return;
        }

        SetFoundLobbiesStatus(_lobbies.Count);

        foreach (Lobby _lobby in _lobbies)
        {
            if (_lobby == null)
                continue;

            if (m_lobbyListItemPrefab == null || m_lobbyListContent == null)
                continue;

            LobbyListItemUI _item = Instantiate(m_lobbyListItemPrefab, m_lobbyListContent);

            _item.Setup(_lobby, OnLobbySelected, OnJoinLobbyClicked);

            m_createdItem.Add(_item);
        }
    }
    #endregion

    #region PrivateFunc
    private void AddLocalizationVariables()
    {
        if (!m_foundLobbiesString.ContainsKey("lobbiesCount"))
            m_foundLobbiesString.Add("lobbiesCount", m_lobbiesCount);

        if (!m_selectedLobbyString.ContainsKey("lobbyName"))
            m_selectedLobbyString.Add("lobbyName", m_lobbyName);
    }

    private void OnLobbySelected(Lobby lobby)
    {
        m_selectedLobby = lobby;

        if (m_selectedLobbyPlayerListUI != null)
        {
            m_selectedLobbyPlayerListUI.ShowLobby(m_selectedLobby);
        }

        if (m_selectedLobby != null)
        {
            SetSelectedLobbyStatus(m_selectedLobby.Name);
        }
    }

    private async void OnJoinLobbyClicked(Lobby lobby)
    {
        if (lobby == null)
            return;

        if (MultiplayerManager.Instance == null)
        {
            Debug.LogError("LobbyListUI: Multiplayer Manager is null");
            return;
        }

        int _version = ++m_requestVersion;

        await MultiplayerManager.Instance.JoinLobbyById(lobby.Id);

        if (this == null)
            return;

        if (m_isClosing || _version != m_requestVersion)
            return;
    }

    private void SetLobbyNotFoundStatus()
    {
        m_statusMode = StatusMode.LobbyNotFound;
        m_lobbyNotFoundString.RefreshString();
    }

    private void SetFoundLobbiesStatus(int lobbiesCount)
    {
        m_statusMode = StatusMode.FoundLobbies;

        m_lobbiesCount.Value = lobbiesCount;

        m_foundLobbiesString.RefreshString();
    }

    private void SetSelectedLobbyStatus(string lobbyName)
    {
        m_statusMode = StatusMode.SelectedLobby;

        m_lobbyName.Value = string.IsNullOrEmpty(lobbyName) ? "Unknown Lobby" : lobbyName;

        m_selectedLobbyString.RefreshString();
    }

    private void OnLobbyNotFoundStringChanged(string value)
    {
        if (m_statusMode != StatusMode.LobbyNotFound)
            return;

        if (m_statusText != null)
        {
            m_statusText.text = value;
        }
    }

    private void OnFoundLobbiesStringChanged(string value)
    {
        if (m_statusMode != StatusMode.FoundLobbies)
            return;

        if (m_statusText != null)
        {
            m_statusText.text = value;
        }
    }

    private void OnSelectedLobbyStringChanged(string value)
    {
        if (m_statusMode != StatusMode.SelectedLobby)
            return;

        if (m_statusText != null)
        {
            m_statusText.text = value;
        }
    }

    private void ClearLobbyList()
    {
        for (int i = m_createdItem.Count - 1; i >= 0; i--)
        {
            if (m_createdItem[i] != null)
            {
                Destroy(m_createdItem[i].gameObject);
            }
        }

        m_createdItem.Clear();

        m_selectedLobby = null;
    }
    #endregion
}