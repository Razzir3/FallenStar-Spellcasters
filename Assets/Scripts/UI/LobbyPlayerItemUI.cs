using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

public class LobbyPlayerItemUI : MonoBehaviour
{
    #region Variable
    [SerializeField]
    private TMP_Text m_playerNameText;

    [SerializeField]
    private TMP_Text m_playerRoleText;

    [SerializeField]
    private TMP_Text m_playerIdText;

    private Player m_currentPlayer;
    private bool m_currentIsHost;
    private bool m_isSetup;

    private int m_setupVersion;
    [SerializeField] private LocalizedString m_unknownPlayerString =
        new LocalizedString("UI_table", "lobby_player_unknown");
    private bool m_isUnknownPlayerName;
    #endregion
    #region Unity
    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }
    #endregion

    #region Setup
    public void Setup(Player player, bool isHost)
    {
        if (player == null)
            return;

        m_currentPlayer = player;
        m_currentIsHost = isHost;
        m_isSetup = true;

        m_setupVersion++;

        string _playerName = GetPlayerDataValue(player, "playerName", null);
        m_isUnknownPlayerName = _playerName == null;
        if (m_isUnknownPlayerName)
            _playerName = m_unknownPlayerString.GetLocalizedString();

        if (m_playerNameText != null)
        {
            m_playerNameText.text = _playerName;
        }

        if (m_playerIdText != null)
        {
            m_playerIdText.text = player.Id;
        }

        SetRoleText(isHost);
    }

    private void SetRoleText(bool isHost)
    {
        if (m_playerRoleText == null)
            return;

        string _roleKey = isHost ? "role_host" : "role_client";

        LocalizedString _roleString = new LocalizedString("UI_table", _roleKey);

        int _currentVersion = m_setupVersion;

        AsyncOperationHandle<string> _handle = _roleString.GetLocalizedStringAsync();

        _handle.Completed += operation =>
        {
            if (this == null)
                return;

            if (_currentVersion != m_setupVersion)
                return;

            if (m_playerRoleText != null)
            {
                m_playerRoleText.text = operation.Result;
            }
        };
    }

    private void OnSelectedLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        if (!m_isSetup)
            return;

        m_setupVersion++;
        if (m_isUnknownPlayerName && m_playerNameText != null)
            m_playerNameText.text = m_unknownPlayerString.GetLocalizedString();
        SetRoleText(m_currentIsHost);
    }

    private string GetPlayerDataValue(Player player, string key, string defaultValue)
    {
        if (player.Data == null)
            return defaultValue;

        if (!player.Data.ContainsKey(key))
            return defaultValue;

        if (player.Data[key] == null)
            return defaultValue;

        if (string.IsNullOrEmpty(player.Data[key].Value))
            return defaultValue;

        return player.Data[key].Value;
    }
    #endregion
}
