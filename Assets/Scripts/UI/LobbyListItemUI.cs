using UnityEngine;
using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public class LobbyListItemUI : MonoBehaviour
{
    #region Variable
    [SerializeField]
    private TMP_Text m_lobbyNameText;

    [SerializeField]
    private TMP_Text m_playerCountText;

    [SerializeField]
    private TMP_Text m_lobbyIdText;

    private Lobby m_lobby;

    private Action<Lobby> m_onSelected;
    private Action<Lobby> m_onJoinClicked;
    [SerializeField] private LocalizedString m_playersCountString =
        new LocalizedString("UI_table", "lobby_players_count");
    [SerializeField] private LocalizedString m_unknownLobbyString =
        new LocalizedString("UI_table", "lobby_name_unknown");
    private readonly IntVariable m_currentPlayers = new IntVariable();
    private readonly IntVariable m_maxPlayers = new IntVariable();
    #endregion

    private void Awake()
    {
        if (!m_playersCountString.ContainsKey("currentPlayers"))
            m_playersCountString.Add("currentPlayers", m_currentPlayers);
        if (!m_playersCountString.ContainsKey("maxPlayers"))
            m_playersCountString.Add("maxPlayers", m_maxPlayers);
    }

    private void OnEnable()
    {
        m_playersCountString.StringChanged += OnPlayersCountStringChanged;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        m_playersCountString.StringChanged -= OnPlayersCountStringChanged;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnPlayersCountStringChanged(string value)
    {
        if (m_lobby != null && m_playerCountText != null)
            m_playerCountText.text = value;
    }

    private void OnSelectedLocaleChanged(Locale locale)
    {
        RefreshUI();
    }

    #region Setup
    public void Setup(Lobby lobby, Action<Lobby> onSelected, Action<Lobby> onJoinClicked)
    {
        m_lobby = lobby;
        m_onSelected = onSelected;
        m_onJoinClicked = onJoinClicked;

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (m_lobby == null)
            return;

        if(m_lobbyNameText != null)
        {
            m_lobbyNameText.text = string.IsNullOrEmpty(m_lobby.Name)
                ? m_unknownLobbyString.GetLocalizedString() : m_lobby.Name;
        }
        m_currentPlayers.Value = m_lobby.Players != null ? m_lobby.Players.Count : 0;
        m_maxPlayers.Value = m_lobby.MaxPlayers;
        m_playersCountString.RefreshString();

        if(m_lobbyIdText != null)
        {
            m_lobbyIdText.text = m_lobby.Id;
        }
    }
    #endregion

    #region ButtonAction
    public void OnClickSelect()
    {
        if (m_lobby == null)
            return;

        m_onSelected?.Invoke(m_lobby);
    }

    public void OnClickJoin()
    {
        if (m_lobby == null)
            return;

        m_onJoinClicked?.Invoke(m_lobby);
    }
    #endregion
}
