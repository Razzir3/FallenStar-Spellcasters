using UnityEngine;
using System;
using TMPro;
using Unity.Services.Lobbies.Models;

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
    #endregion

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
            m_lobbyNameText.text = m_lobby.Name;
        }
        if(m_playerCountText != null)
        {
            m_playerCountText.text = m_lobby.Players.Count + " / " + m_lobby.MaxPlayers;
        }

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
