using Unity.Netcode;
using UnityEngine;

public partial class GameMaster
{
    public void HandlePlayerSurrender(PlayerSession session)
    {
        if (!IsServer || session == null)
            return;

        if (m_gameCoroutine == null || m_gamePhase.Value == GamePhase.None ||
            m_gamePhase.Value == GamePhase.GameOver)
            return;

        if (!m_players.Contains(session))
            return;

        StopCoroutine(m_gameCoroutine);
        m_gameCoroutine = null;
        m_isResolvingCards = false;
        m_isWaitingRespawn = false;

        PlayerSession winner = null;
        foreach (PlayerSession player in m_players)
        {
            if (player != null && player != session && player.TeamId.Value != -1)
            {
                winner = player;
                break;
            }
        }

        if (winner == null)
        {
            AbortMatch("player_surrendered_no_winner");
            return;
        }

        EndMatch(winner, MatchEndReason.Surrendered);

        if (m_debugMode)
            Debug.Log($"GameMaster: player {session.OwnerClientId} surrendered.");
    }

    public void HandlePlayerLeft(PlayerSession session)
    {
        if (!IsServer || session == null)
            return;

        if (m_gameCoroutine == null || m_gamePhase.Value == GamePhase.None ||
            m_gamePhase.Value == GamePhase.GameOver)
            return;

        if (!m_players.Contains(session))
            return;

        StopCoroutine(m_gameCoroutine);
        m_gameCoroutine = null;

        if (m_debugMode)
        {
            SuspiciousActivityLog _leavingLog = session.GetSuspiciousLog();
            if (_leavingLog.Total > 0)
                Debug.Log($"[Suspicious] client {session.OwnerClientId} match summary: {_leavingLog.ToSummaryString()}");
        }

        m_players.Remove(session);
        m_turnOrder.Remove(session);
        m_turnSelections.Remove(session);
        m_isResolvingCards = false;
        m_isWaitingRespawn = false;

        PlayerSession winner = null;
        foreach (PlayerSession player in m_players)
        {
            if (player != null && player.TeamId.Value != -1)
            {
                winner = player;
                break;
            }
        }

        if (winner == null)
        {
            AbortMatch("player_left_no_winner");
            return;
        }

        EndMatch(winner, MatchEndReason.OpponentDisconnected);

        if (m_debugMode)
        {
            Debug.Log("GameMaster: opponent disconnected. Remaining player wins by forfeit.");
        }
    }

    /// <summary>
    /// Returns the PlayerSession component of the NetworkObject with the given networkObjectId.
    /// </summary>
    /// <param name="networkObjectId">The network object ID of the player session.</param>
    /// <returns>The PlayerSession component if found; otherwise, null.</returns>
    public PlayerSession GetSessionByID(ulong networkObjectId)
    {
        PlayerSession _result = null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject _networkObject))
        {
            try
            {
                _result = _networkObject.GetComponent<PlayerSession>();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        return _result;
    }

    //todo
    /*
    private PlayerController GetCharacterByID(ulong networkObjectId)
    {
        PlayerController _result = null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject _networkObject))
        {
            try
            {
                _result = _networkObject.GetComponent<PlayerController>();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        return _result;
    }
    */
}
