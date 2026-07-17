using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public class LobbyPlayerListUI : MonoBehaviour
{
    #region Variable
    [SerializeField]
    private Transform m_playerListContent;

    [SerializeField]
    private LobbyPlayerItemUI m_playerItemPrefab;

    [SerializeField]
    private TMP_Text m_statusText;

    [Header("Localization")]
    [SerializeField]
    private LocalizedString m_noLobbySelectedString =
        new LocalizedString("UI_table", "lobby_no_selected");

    [SerializeField]
    private LocalizedString m_playersCountString =
        new LocalizedString("UI_table", "lobby_players_count");

    private readonly IntVariable m_currentPlayers = new IntVariable();
    private readonly IntVariable m_maxPlayers = new IntVariable();

    private readonly List<LobbyPlayerItemUI> m_createdItems = new List<LobbyPlayerItemUI>();

    private enum StatusMode
    {
        None,
        NoLobbySelected,
        PlayersCount
    }

    private StatusMode m_statusMode = StatusMode.None;
    #endregion

    #region Unity
    private void Awake()
    {
        AddPlayersCountVariables();
    }

    private void OnEnable()
    {
        m_noLobbySelectedString.StringChanged += OnNoLobbySelectedStringChanged;
        m_playersCountString.StringChanged += OnPlayersCountStringChanged;
    }

    private void OnDisable()
    {
        m_noLobbySelectedString.StringChanged -= OnNoLobbySelectedStringChanged;
        m_playersCountString.StringChanged -= OnPlayersCountStringChanged;
    }
    #endregion

    #region PublicFunc
    public void ShowLobby(Lobby lobby)
    {
        Clear();

        if (lobby == null)
        {
            SetNoLobbySelectedStatus();
            return;
        }

        int _playersCount = lobby.Players != null ? lobby.Players.Count : 0;
        int _maxPlayers = lobby.MaxPlayers;

        SetPlayersCountStatus(_playersCount, _maxPlayers);

        if (lobby.Players == null)
            return;

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            Player _player = lobby.Players[i];

            if (_player == null)
                continue;

            if (m_playerItemPrefab == null || m_playerListContent == null)
                continue;

            LobbyPlayerItemUI _item = Instantiate(m_playerItemPrefab, m_playerListContent);

            bool _isHost = _player.Id == lobby.HostId;

            _item.Setup(_player, _isHost);

            m_createdItems.Add(_item);
        }
    }
    #endregion

    #region PrivateFunc
    private void AddPlayersCountVariables()
    {
        if (!m_playersCountString.ContainsKey("currentPlayers"))
            m_playersCountString.Add("currentPlayers", m_currentPlayers);

        if (!m_playersCountString.ContainsKey("maxPlayers"))
            m_playersCountString.Add("maxPlayers", m_maxPlayers);
    }

    private void SetNoLobbySelectedStatus()
    {
        m_statusMode = StatusMode.NoLobbySelected;
        m_noLobbySelectedString.RefreshString();
    }

    private void SetPlayersCountStatus(int currentPlayers, int maxPlayers)
    {
        m_statusMode = StatusMode.PlayersCount;

        m_currentPlayers.Value = currentPlayers;
        m_maxPlayers.Value = maxPlayers;

        m_playersCountString.RefreshString();
    }

    private void OnNoLobbySelectedStringChanged(string value)
    {
        if (m_statusMode != StatusMode.NoLobbySelected)
            return;

        if (m_statusText != null)
        {
            m_statusText.text = value;
        }
    }

    private void OnPlayersCountStringChanged(string value)
    {
        if (m_statusMode != StatusMode.PlayersCount)
            return;

        if (m_statusText != null)
        {
            m_statusText.text = value;
        }
    }

    private void Clear()
    {
        for (int i = m_createdItems.Count - 1; i >= 0; i--)
        {
            if (m_createdItems[i] != null)
            {
                Destroy(m_createdItems[i].gameObject);
            }
        }

        m_createdItems.Clear();
    }
    #endregion
}