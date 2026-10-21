using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CardWorldViewManager : MonoBehaviour
{
    #region Variable

    public static CardWorldViewManager Instance;

    [Header("Prefab")]
    [Header("Prefab")]
    [SerializeField]
    private CardViewWorld m_cardWorldViewPrefab;


    private readonly Dictionary<ulong, PlayerCardWorldArea> m_playerAreas =
        new Dictionary<ulong, PlayerCardWorldArea>();


    private readonly Dictionary<ulong, CardViewWorld> m_spawnedViews =
        new Dictionary<ulong, CardViewWorld>();

    private readonly HashSet<PlayerSession> m_registeredSessions =
        new HashSet<PlayerSession>();
    private readonly List<PlayerSession> m_sessionsToUnregister = new();

    [SerializeField]
    private bool m_debugMode = false;

    #endregion

    #region Unity Calling Func

    private void Awake()
    {
        m_debugMode = DebugModeGuard.Allow(m_debugMode);
        Debug.Log(
            $"CardWorldViewManager Awake called on {gameObject.name}.");

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Duplicate CardWorldViewManager was destroyed.");

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Debug.Log(
            $"CardWorldViewManager Start called on {gameObject.name}.");

        FindAreas();

        StartCoroutine(
            WaitForSessions());
    }

    private void OnDestroy()
    {
        UnregisterAllSessions();
        ClearAllWorldViews();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (SessionManager.Instance == null)
            return;

        m_sessionsToUnregister.Clear();
        foreach (PlayerSession session in m_registeredSessions)
        {
            if (session == null || !session.IsSpawned ||
                !SessionManager.Instance.m_playerSessions.ContainsValue(session))
                m_sessionsToUnregister.Add(session);
        }
        foreach (PlayerSession session in m_sessionsToUnregister)
            UnregisterSession(session);

        if (SessionManager.Instance.m_playerSessions.Count ==
            m_registeredSessions.Count)
        {
            return;
        }

        RegisterAllSessions();
        FindAreas();
        RefreshAllSessions();
    }

    #endregion

    #region Area Registration

    private void FindAreas()
    {
        PlayerCardWorldArea[] areas =
            FindObjectsByType<PlayerCardWorldArea>(
                FindObjectsSortMode.None);

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: found areas: {areas.Length}");
        }

        foreach (PlayerCardWorldArea area in areas)
        {
            if (area == null)
                continue;

            PlayerSession playerSession =
                area.GetPlayerSession();

            if (playerSession == null &&
                SessionManager.Instance != null)
            {
                foreach (var pair in
                         SessionManager.Instance.m_playerSessions)
                {
                    PlayerSession session = pair.Value;

                    if (session == null)
                        continue;

                    if (!session.IsSpawned)
                        continue;

                    if (session.TeamId.Value ==
                        area.m_sessionSearchId)
                    {
                        area.Initialize(session);
                        playerSession = session;
                        break;
                    }
                }
            }

            if (playerSession != null)
            {
                RegisterArea(area);
            }
            else if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: session was not found " +
                    $"for area {area.name}. " +
                    $"Search TeamId: {area.m_sessionSearchId}");
            }
        }
    }

    public void RegisterArea(PlayerCardWorldArea area)
    {
        if (area == null)
            return;

        PlayerSession playerSession =
            area.GetPlayerSession();

        if (playerSession == null)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: " +
                    $"PlayerCardWorldArea {area.name} " +
                    $"does not have PlayerSession.");
            }

            return;
        }

        if (!playerSession.IsSpawned)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: " +
                    $"PlayerSession for area {area.name} " +
                    $"is not spawned.");
            }

            return;
        }

        ulong sessionNetworkObjectId =
            area.GetSessionNetworkObjectId();

        if (sessionNetworkObjectId == 0)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: " +
                    $"invalid PlayerSession NetworkObjectId " +
                    $"for area {area.name}.");
            }

            return;
        }

        m_playerAreas[sessionNetworkObjectId] = area;

        RegisterSession(playerSession);

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: area {area.name} " +
                $"registered for PlayerSession " +
                $"{sessionNetworkObjectId}.");
        }

        RefreshAllSessions();
    }

    public void UnregisterArea(PlayerCardWorldArea area)
    {
        if (area == null)
            return;

        ulong sessionNetworkObjectId =
            area.GetSessionNetworkObjectId();

        if (sessionNetworkObjectId == 0)
            return;

        if (!m_playerAreas.TryGetValue(
                sessionNetworkObjectId,
                out PlayerCardWorldArea currentArea))
        {
            return;
        }

        if (currentArea != area)
            return;

        m_playerAreas.Remove(sessionNetworkObjectId);

        RefreshAllSessions();
    }

    #endregion

    #region Session Registration

    private IEnumerator WaitForSessions()
    {
        while (SessionManager.Instance == null)
        {
            if (m_debugMode)
            {
                Debug.Log(
                    "CardWorldViewManager: waiting for SessionManager.");
            }

            yield return null;
        }

        while (SessionManager.Instance.m_playerSessions.Count == 0)
        {
            if (m_debugMode)
            {
                Debug.Log(
                    "CardWorldViewManager: waiting for PlayerSession objects.");
            }

            yield return null;
        }

        RegisterAllSessions();
        FindAreas();
        RefreshAllSessions();

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager initialized. " +
                $"Sessions: {m_registeredSessions.Count}, " +
                $"Areas: {m_playerAreas.Count}");
        }
    }

    private void RegisterAllSessions()
    {
        if (SessionManager.Instance == null)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    "CardWorldViewManager: " +
                    "SessionManager.Instance is null.");
            }

            return;
        }

        foreach (var pair in
                 SessionManager.Instance.m_playerSessions)
        {
            PlayerSession playerSession =
                pair.Value;

            if (playerSession == null)
                continue;

            if (!playerSession.IsSpawned)
                continue;

            RegisterSession(
                playerSession);
        }
    }

    public void RegisterSession(PlayerSession session)
    {
        if (session == null)
            return;

        if (!session.IsSpawned)
            return;

        if (m_registeredSessions.Contains(session))
            return;

        session.m_boardPlayCards.OnListChanged +=
            OnBoardCardListChanged;

        session.m_boardDiscardCards.OnListChanged +=
            OnBoardCardListChanged;

        m_registeredSessions.Add(session);

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: PlayerSession " +
                $"{session.NetworkObjectId} registered. " +
                $"TeamId: {session.TeamId.Value}");
        }
    }   

    public void UnregisterSession(PlayerSession session)
    {
        if (session == null)
        {
            if (m_registeredSessions.Remove(session))
                RefreshAllSessions();
            return;
        }

        if (!m_registeredSessions.Contains(session))
            return;

        session.m_boardPlayCards.OnListChanged -=
            OnBoardCardListChanged;

        session.m_boardDiscardCards.OnListChanged -=
            OnBoardCardListChanged;

        m_registeredSessions.Remove(session);

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: PlayerSession " +
                $"{session.NetworkObjectId} unregistered.");
        }

        RefreshAllSessions();
    }

    private void UnregisterAllSessions()
    {
        PlayerSession[] sessions =
            new PlayerSession[m_registeredSessions.Count];

        m_registeredSessions.CopyTo(sessions);

        foreach (PlayerSession session in sessions)
        {
            if (session == null)
                continue;

            session.m_boardPlayCards.OnListChanged -=
                OnBoardCardListChanged;

            session.m_boardDiscardCards.OnListChanged -=
                OnBoardCardListChanged;
        }

        m_registeredSessions.Clear();
    }

    #endregion

    #region NetworkList Callback

    private void OnBoardCardListChanged(
        NetworkListEvent<ulong> changeEvent)
    {
        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: board list changed. " +
                $"Type: {changeEvent.Type}, " +
                $"Index: {changeEvent.Index}, " +
                $"Value: {changeEvent.Value}");
        }

        RefreshAllSessions();
    }

    #endregion

    #region Refresh

    public void RefreshAllSessions()
    {
        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager.RefreshAllSessions called. " +
                $"Registered sessions: {m_registeredSessions.Count}, " +
                $"Registered areas: {m_playerAreas.Count}");
        }

        HashSet<ulong> activeCardIds =
            new HashSet<ulong>();

        foreach (PlayerSession session
                 in m_registeredSessions)
        {
            if (session == null)
                continue;

            if (!session.IsSpawned)
                continue;

            RefreshSession(
                session,
                activeCardIds);
        }

        RemoveUnusedViews(
            activeCardIds);
    }

    private void RefreshSession(
    PlayerSession session,
    HashSet<ulong> activeCardIds)
    {
        if (session == null)
            return;

        PlayerCardWorldArea area = null;

        if (!m_playerAreas.TryGetValue(
                session.NetworkObjectId,
                out area))
        {
            if (GameMaster.Instance != null)
            {
                area =
                    GameMaster.Instance.GetPlayerCardWorldArea(
                        session);
            }

            if (area == null)
            {
                PlayerCardWorldArea[] areas =
                    FindObjectsByType<PlayerCardWorldArea>(
                        FindObjectsSortMode.None);

                foreach (PlayerCardWorldArea foundedArea
                         in areas)
                {
                    if (foundedArea == null)
                        continue;

                    if (foundedArea.m_sessionSearchId !=
                        session.TeamId.Value)
                    {
                        continue;
                    }

                    foundedArea.Initialize(session);
                    area = foundedArea;
                    break;
                }
            }

            if (area != null)
            {
                m_playerAreas[session.NetworkObjectId] =
                    area;
            }
        }

        if (area == null)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: " +
                    $"PlayerCardWorldArea was not found for " +
                    $"PlayerSession {session.NetworkObjectId}, " +
                    $"TeamId {session.TeamId.Value}.");
            }

            return;
        }

        int cardCount =
            session.m_boardPlayCards.Count +
            session.m_boardDiscardCards.Count;

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: refreshing session " +
                $"{session.NetworkObjectId}. " +
                $"Play cards: {session.m_boardPlayCards.Count}, " +
                $"Discard cards: {session.m_boardDiscardCards.Count}, " +
                $"Total: {cardCount}");
        }

        int slotIndex = 0;

        for (int i = 0;
             i < session.m_boardPlayCards.Count;
             i++)
        {
            ulong cardNetworkObjectId =
                session.m_boardPlayCards[i];

            AddCardToSlot(
                cardNetworkObjectId,
                area,
                slotIndex,
                cardCount,
                activeCardIds);

            slotIndex++;
        }

        for (int i = 0;
             i < session.m_boardDiscardCards.Count;
             i++)
        {
            ulong cardNetworkObjectId =
                session.m_boardDiscardCards[i];

            AddCardToSlot(
                cardNetworkObjectId,
                area,
                slotIndex,
                cardCount,
                activeCardIds);

            slotIndex++;
        }
    }

    private void AddCardToSlot(
    ulong cardNetworkObjectId,
    PlayerCardWorldArea area,
    int slotIndex,
    int cardCount,
    HashSet<ulong> activeCardIds)
    {
        if (cardNetworkObjectId == 0)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    "CardWorldViewManager: card ID is 0.");
            }

            return;
        }

        if (area == null)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    "CardWorldViewManager: area is null.");
            }

            return;
        }

        NetworkCard card =
            GetNetworkCard(cardNetworkObjectId);

        if (card == null)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: NetworkCard " +
                    $"{cardNetworkObjectId} was not found yet.");
            }

            return;
        }
        activeCardIds.Add(cardNetworkObjectId);

        Vector3 cardLocalPosition =
            area.GetCardLocalPosition(
                slotIndex,
                cardCount);

        CreateOrMoveView(
            card,
            area.transform,
            cardLocalPosition);
    }

    #endregion

    #region View Creation

    private void CreateOrMoveView(
    NetworkCard card,
    Transform slot,
    Vector3 cardLocalPosition)
    {
        if (card == null)
            return;

        if (slot == null)
            return;

        if (card.m_cardData == null)
        {
            if (m_debugMode)
            {
                Debug.LogWarning(
                    $"CardWorldViewManager: CardData is null " +
                    $"for NetworkCard {card.NetworkObjectId}. " +
                    $"Waiting for card data.");
            }

            return;
        }

        if (!m_spawnedViews.TryGetValue(
                card.NetworkObjectId,
                out CardViewWorld view))
        {
            if (m_cardWorldViewPrefab == null)
            {
                Debug.LogError(
                    "CardWorldViewManager: " +
                    "m_cardWorldViewPrefab is null.");

                return;
            }

            view = Instantiate(
                m_cardWorldViewPrefab,
                slot);

            view.Setup(card.m_cardData);
            view.SetNetworkCard(card);

            m_spawnedViews.Add(
                card.NetworkObjectId,
                view);

            card.SetAdditionalView(view);

            if (m_debugMode)
            {
                Debug.Log(
                    $"CardWorldViewManager: CardViewWorld " +
                    $"created for NetworkCard " +
                    $"{card.NetworkObjectId}.");
            }
        }
        else
        {
            view.transform.SetParent(
                slot,
                false);
        }

        view.transform.localPosition =
            cardLocalPosition;

        view.transform.localRotation =
            Quaternion.identity;

        view.transform.localScale =
            Vector3.one;

        view.SetFaceUp(
            card.m_isFaceUp.Value);

        view.SetRotated(
            card.m_isRotated.Value);
    }
    #endregion

    #region View Removing

    private void RemoveUnusedViews(
        HashSet<ulong> activeCardIds)
    {
        List<ulong> cardsToRemove =
            new List<ulong>();

        foreach (var pair in m_spawnedViews)
        {
            ulong cardNetworkObjectId = pair.Key;

            if (!activeCardIds.Contains(
                    cardNetworkObjectId))
            {
                cardsToRemove.Add(
                    cardNetworkObjectId);
            }
        }

        foreach (ulong cardNetworkObjectId
                 in cardsToRemove)
        {
            RemoveWorldView(
                cardNetworkObjectId);
        }
    }

    public void RemoveWorldView(
        ulong cardNetworkObjectId)
    {
        if (!m_spawnedViews.TryGetValue(
                cardNetworkObjectId,
                out CardViewWorld view))
        {
            return;
        }

        NetworkCard card =
            GetNetworkCard(cardNetworkObjectId);

        if (card != null)
        {
            /*
             * m_currentView не изменяется.
             */
            card.SetAdditionalView(null);
        }

        if (view != null)
        {
            Destroy(view.gameObject);
        }

        m_spawnedViews.Remove(
            cardNetworkObjectId);

        if (m_debugMode)
        {
            Debug.Log(
                $"CardWorldViewManager: CardViewWorld " +
                $"removed for NetworkCard " +
                $"{cardNetworkObjectId}.");
        }
    }

    public void ClearAllWorldViews()
    {
        List<ulong> cardsToRemove =
            new List<ulong>(m_spawnedViews.Keys);

        foreach (ulong cardNetworkObjectId
                 in cardsToRemove)
        {
            RemoveWorldView(
                cardNetworkObjectId);
        }

        m_spawnedViews.Clear();
    }

    #endregion

    #region Search Func

    private NetworkCard GetNetworkCard(
        ulong networkObjectId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        if (NetworkManager.Singleton.SpawnManager == null)
            return null;

        if (!NetworkManager.Singleton.SpawnManager
            .SpawnedObjects.TryGetValue(
                networkObjectId,
                out NetworkObject networkObject))
        {
            return null;
        }

        if (networkObject == null)
            return null;

        return networkObject.GetComponent<NetworkCard>();
    }

    #endregion
}