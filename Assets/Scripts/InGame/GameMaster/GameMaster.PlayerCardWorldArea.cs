using UnityEngine;

public partial class GameMaster
{
    /// <summary>
    /// Searches for all PlayerCardWorldArea objects in the scene and stores them in the m_playerCardWorldAreas array.
    /// </summary>
    private void FindPlayerCardWorldAreas()
    {
        m_playerCardWorldAreas =
            FindObjectsByType<PlayerCardWorldArea>(FindObjectsSortMode.None);

        if (m_playerCardWorldAreas == null ||
            m_playerCardWorldAreas.Length == 0)
        {
            Debug.LogWarning("PlayerCardWorldArea objects were not found on the scene.");

            return;
        }

        if (m_debugMode)
        {
            Debug.Log(
                $"Found PlayerCardWorldArea objects: " +
                $"{m_playerCardWorldAreas.Length}");
        }
    }
    /// <summary>
    /// Binds each PlayerCardWorldArea to its corresponding PlayerSession based on the session search ID.
    /// </summary>
    private void BindPlayerCardWorldAreas()
    {
        if (!IsServer)
            return;

        if (m_playerCardWorldAreas == null ||
            m_playerCardWorldAreas.Length == 0)
        {
            FindPlayerCardWorldAreas();
        }

        if (m_playerCardWorldAreas == null ||
            m_playerCardWorldAreas.Length == 0)
        {
            Debug.LogWarning("Cannot bind PlayerCardWorldArea: no areas found.");

            return;
        }

        if (SessionManager.Instance == null)
        {
            Debug.LogError(
                "Cannot bind PlayerCardWorldArea: " +
                "SessionManager.Instance is null.");

            return;
        }

        foreach (PlayerCardWorldArea area in m_playerCardWorldAreas)
        {
            if (area == null)
                continue;

            PlayerSession foundedSession = FindSessionForCardWorldArea(area.m_sessionSearchId);

            if (foundedSession == null)
            {
                Debug.LogWarning(
                    $"PlayerSession with TeamId " +
                    $"{area.m_sessionSearchId} was not found for " +
                    $"PlayerCardWorldArea {area.name}.");

                area.ClearPlayerSession();
                continue;
            }

            area.Initialize(foundedSession);

            if (m_debugMode)
            {
                Debug.Log(
                    $"PlayerCardWorldArea {area.name} bound to " +
                    $"PlayerSession {foundedSession.NetworkObjectId}. " +
                    $"TeamId: {foundedSession.TeamId.Value}");
            }
        }
    }
    /// <summary>
    /// Searches for a PlayerSession that matches the given session search ID.
    /// </summary>
    /// <param name="sessionSearchId">The session search ID to match.</param>
    /// <returns>The matching PlayerSession, or null if not found.</returns>
    private PlayerSession FindSessionForCardWorldArea(int sessionSearchId)
    {
        if (SessionManager.Instance == null)
            return null;

        foreach (var pair in SessionManager.Instance.m_playerSessions)
        {
            PlayerSession session = pair.Value;

            if (session == null)
                continue;

            if (!session.IsSpawned)
                continue;

            if (session.TeamId.Value == sessionSearchId)
            {
                return session;
            }
        }

        return null;
    }
    
    /// <summary>
    /// Retrieves the PlayerCardWorldArea associated with the given PlayerSession.
    /// </summary>
    /// <param name="playerSession">The PlayerSession to find the corresponding PlayerCardWorldArea for.</param>
    /// <returns>The matching PlayerCardWorldArea, or null if not found.</returns>
    public PlayerCardWorldArea GetPlayerCardWorldArea(PlayerSession playerSession)
    {
        if (playerSession == null)
            return null;


        if (m_playerCardWorldAreas != null)
        {
            foreach (PlayerCardWorldArea area
                     in m_playerCardWorldAreas)
            {
                if (area == null)
                    continue;

                if (area.GetPlayerSession() ==
                    playerSession)
                {
                    return area;
                }


                if (area.m_sessionSearchId ==
                    playerSession.TeamId.Value)
                {
                    area.Initialize(playerSession);
                    return area;
                }
            }
        }

        FindPlayerCardWorldAreas();

        if (m_playerCardWorldAreas == null)
            return null;

        foreach (PlayerCardWorldArea area
                 in m_playerCardWorldAreas)
        {
            if (area == null)
                continue;

            if (area.GetPlayerSession() ==
                playerSession)
            {
                return area;
            }

            if (area.m_sessionSearchId ==
                playerSession.TeamId.Value)
            {
                area.Initialize(playerSession);
                return area;
            }
        }

        Debug.LogWarning(
            $"PlayerCardWorldArea was not found for " +
            $"TeamId {playerSession.TeamId.Value}.");

        return null;
    }
    /// <summary>
    /// Retrieves the PlayerCardWorldArea associated with the given PlayerSession's network object ID.
    /// </summary>
    /// <param name="playerSessionNetworkObjectId">The network object ID of the PlayerSession to find the corresponding PlayerCardWorldArea for.</param>
    /// <returns>The matching PlayerCardWorldArea, or null if not found.</returns>
    public PlayerCardWorldArea GetPlayerCardWorldArea(ulong playerSessionNetworkObjectId)
    {
        if (playerSessionNetworkObjectId == 0)
            return null;

        if (m_playerCardWorldAreas == null ||
            m_playerCardWorldAreas.Length == 0)
        {
            FindPlayerCardWorldAreas();
        }

        foreach (PlayerCardWorldArea area in m_playerCardWorldAreas)
        {
            if (area == null)
                continue;

            if (area.GetSessionNetworkObjectId() ==
                playerSessionNetworkObjectId)
            {
                return area;
            }
        }

        return null;
    }
    
    /// <summary>
    /// Clears all PlayerCardWorldAreas.
    /// </summary>
    private void ClearPlayerCardWorldAreas()
    {
        if (m_playerCardWorldAreas == null)
            return;

        foreach (PlayerCardWorldArea area in m_playerCardWorldAreas)
        {
            if (area == null)
                continue;

            area.ClearPlayerSession();
        }
    }

}
