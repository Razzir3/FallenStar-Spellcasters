using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.CompilerServices;
public enum GamePhase
{
    None,
    RoundBreak,
    DrawCards,
    CardSelection,
    PlayerAction,
    ResolveCards,
    RespawnWait,
    RoundEnd
}

public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        for (int i = list.Count() - 1; i > 0; i--)
        {
            int randomSeed = Random.Range(0, i + 1);

            (list[i], list[randomSeed]) = (list[randomSeed], list[i]);
        }
    }
}

public partial class GameMaster : NetworkBehaviour
{
    #region Variable
    public static GameMaster Instance;

    public bool m_debugMode = false;

    private Queue<PlayerControlPackage> m_inputQueue = new Queue<PlayerControlPackage>();
    private Queue<CardControllPackage> m_cardInputQueue = new Queue<CardControllPackage>();

    [SerializeField] private GameObject m_playerCharacterPrefab;
    [SerializeField] private GameObject m_blockPrefab;

    [SerializeField] private NetworkCard m_cardPrefab;
    [SerializeField] private CardDatabase m_cardDatabase;
    [SerializeField] private GameObject m_cardViewWorldPrefab;

    [SerializeField]
    private PlayerCardWorldArea[] m_playerCardWorldAreas;

    private List<NetworkCard> m_spawnedCards = new();
    private List<NetworkCard> m_deck = new();
    private List<NetworkCard> m_graveyard = new();

    private SpawnPoint[] m_spawnPoint;


    [Header("Timing")]
    [SerializeField] private float m_roundBreakTime = 5f;
    [SerializeField] private float m_cardDrawDelay = 0.8f;
    [SerializeField] private float m_cardSelectionTime = 60f;
    [SerializeField] private float m_playerActionTime = 60f;
    [SerializeField] private float m_cardResolveDelay = 3f;
    [SerializeField] private float m_respawnWaitTime = 30f;
    [Header("Movement")]
    [SerializeField]
    private float m_stepMoveDistance = 3f;
    [Header("GameRules")]
    [SerializeField] private int m_startHandCards = 2;

    public NetworkVariable <GamePhase> m_gamePhase = new NetworkVariable<GamePhase>(GamePhase.None, NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);

    private List<PlayerSession> m_players = new();
    private readonly Dictionary<PlayerSession, PlayerTurnSelection> m_turnSelections = new();

    public NetworkVariable<NetworkBehaviourReference> m_activePlayer;
    public NetworkVariable<NetworkBehaviourReference> m_waitingRespawnPlayer;
    

    private bool m_isResolvingCards;
    private bool m_isWaitingRespawn;

    private Coroutine m_gameLoopCoroutine;
    private Coroutine m_phasesTimerCoroutine;

    private List<PlayerSession> m_turnOrder = new();

    private bool m_activePlayerConfirmedAction;
    private bool m_respawnConfirmed;

    private ulong m_respawnBlockNetworkId = 0;

    private Coroutine m_gameCoroutine;

    public NetworkVariable<float> m_timer;

    private bool m_externalDeathDetected = false;

    private bool m_isLocalPlayerLost = false;

    private ulong m_lostClientId = 0;
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        FindSpawnPoints();
        HideSpawnPoints();

        FindPlayerCardWorldAreas();
    }
    void Update()
    {
        
        if (NetworkManager.Singleton == null)
            return;
        if (!NetworkManager.Singleton.IsListening)
            return;
        //Only Server can handle the input packages, so we check if we are the host before processing the queue
        if (!IsHost) return;
        
        InputQueueUpdate();
        //CardMessageQueueUpdate();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Instance = this;

        if (m_debugMode)
        {
            Debug.Log(
                $"GameMaster spawned. " +
                $"NetworkObjectId: {NetworkObjectId}, " +
                $"IsServer: {IsServer}, " +
                $"IsClient: {IsClient}");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnNetworkDespawn();
    }
    #endregion

    #region Finders, Getters and Clearers
    private bool ContainsNetworkObjectId(NetworkList<ulong> networkList, ulong networkObjectId)
    {
        for (int i = 0; i < networkList.Count; i++)
        {
            if (networkList[i] == networkObjectId)
            {
                return true;
            }
        }

        return false;
    }
    private void RemoveNetworkObjectId(NetworkList<ulong> networkList, ulong networkObjectId)
    {
        if (!IsServer)
            return;

        for (int i = networkList.Count - 1; i >= 0; i--)
        {
            if (networkList[i] == networkObjectId)
            {
                networkList.RemoveAt(i);
            }
        }
    }
    #region Block
    private BlockController GetBlockById(ulong networkObjectId)
    {
        BlockController _result = null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject _networkObject))
        {
            try
            {
                _result = _networkObject.GetComponent<BlockController>();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        return _result;
    }
    #endregion

    #region SpawnPoint
    private bool FindSpawnPoints()
    {
        bool _result = false;

        m_spawnPoint = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

        if (m_spawnPoint != null)
        {
            _result = true;
        }

        return _result;
    }


    public List<SpawnPoint> GetRespawnPoints(PlayerSession session)
    {
        List<SpawnPoint> _result =
            new List<SpawnPoint>();

        if (session == null)
            return _result;

        BlockController[,] _sessionBlocks =
            session.GetSessionBlock();

        if (_sessionBlocks == null)
            return _result;

        foreach (BlockController _block in _sessionBlocks)
        {
            if (_block == null)
                continue;

            if (_block.NetworkObject == null)
                continue;

            if (!_block.NetworkObject.IsSpawned)
                continue;

            SpawnPoint _spawnPoint =
                _block.GetSpawn();

            if (_spawnPoint == null)
                continue;
            if (_block.m_teamId.Value != session.TeamId.Value)
                continue;

            _result.Add(_spawnPoint);
        }

        return _result;
    }

    public SpawnPoint GetSpawnPoint(int teamId)
    {
        if (m_spawnPoint == null)
        {
            bool _exist = FindSpawnPoints();
            if (!_exist) return null;
        }
        List<SpawnPoint> _validPoints = new();

        foreach (SpawnPoint _point in m_spawnPoint)
        {
            if (_point == null)
            {
                continue;
            }

            if (!_point.IsEnabled)
            {
                continue;
            }

            if (_point.IsOccupied)
            {
                continue;
            }

            if ((_point.TeamId != teamId))
            {
                continue;
            }

            _validPoints.Add(_point);
        }

        if ((_validPoints.Count == 0))
        {
            return null;
        }

        int _index = Random.Range(0, _validPoints.Count);

        return _validPoints[_index];
    }
    #endregion

    public void ClearupAfterDisconnect()
    {
        if (m_gameCoroutine != null)
        {
            StopCoroutine(m_gameCoroutine);
            m_gameCoroutine = null;
        }

        m_inputQueue.Clear();
        m_cardInputQueue.Clear();

        m_players.Clear();
        m_turnOrder.Clear();
        m_turnSelections.Clear();

        m_deck.Clear();
        m_graveyard.Clear();
        m_spawnedCards.Clear();

        m_isResolvingCards = false;
        m_isWaitingRespawn = false;
        m_activePlayerConfirmedAction = false;
        m_respawnConfirmed = false;
        m_externalDeathDetected = false;
        m_respawnBlockNetworkId = 0;

        if (m_spawnPoint != null)
        {
            foreach (var _spawnPoint in m_spawnPoint)
            {
                if (_spawnPoint == null)
                    continue;

                _spawnPoint.IsOccupied = false;
            }
        }

        if (SessionManager.Instance != null)
        {
            foreach (var _playerSession in
                     SessionManager.Instance.m_playerSessions)
            {
                if (_playerSession.Value == null)
                    continue;

                _playerSession.Value.ClearSession();
            }
        }

        ClearPlayerCardWorldAreas();

        if (CardWorldViewManager.Instance != null)
        {
            CardWorldViewManager.Instance.ClearAllWorldViews();
        }

        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.ClearLocalUI();
        }
    }

    public void DespawnAllCards()
    {
        if (!IsServer)
            return;

        for (int i = m_spawnedCards.Count - 1; i >= 0; i--)
        {
            NetworkCard card = m_spawnedCards[i];

            if (card != null && card.NetworkObject.IsSpawned)
            {
                card.NetworkObject.Despawn(true);
            }
        }

        m_spawnedCards.Clear();
    }
    #endregion

    #region Visual Utility
    private void HideSpawnPoints()
    {
        foreach (var _spawnPoint in m_spawnPoint)
        {
            Renderer[] _renderers = _spawnPoint.GetComponentsInChildren<Renderer>();

            foreach (Renderer __render in _renderers)
            {
                __render.enabled = false;
            }
        }
    }
    #endregion
    public void StartNewGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "GameMaster.StartNewGame can only be called on server.");

            return;
        }

        if (m_gameCoroutine != null)
        {
            StopCoroutine(
                m_gameCoroutine);

            m_gameCoroutine = null;
        }

        DespawnAllCards();

        m_players.Clear();
        m_turnOrder.Clear();
        m_turnSelections.Clear();

        m_inputQueue.Clear();
        m_cardInputQueue.Clear();

        m_deck.Clear();
        m_graveyard.Clear();

        m_isResolvingCards = false;
        m_isWaitingRespawn = false;
        m_activePlayerConfirmedAction = false;
        m_respawnConfirmed = false;

        m_activePlayer.Value = default;
        m_waitingRespawnPlayer.Value = default;

        m_timer.Value = 0f;
        m_gamePhase.Value = GamePhase.None;

        if (SessionManager.Instance == null)
        {
            Debug.LogError(
                "GameMaster.StartNewGame: SessionManager.Instance is null.");

            return;
        }

        foreach (var pair in
                 SessionManager.Instance.m_playerSessions)
        {
            PlayerSession player =
                pair.Value;

            if (player == null)
                continue;

            player.m_handCards.Clear();
            player.m_boardPlayCards.Clear();
            player.m_boardDiscardCards.Clear();

            player.ClearSelectedCard();

            m_players.Add(player);
        }

        if (m_players.Count != 2)
        {
            Debug.LogWarning(
                $"GameMaster.StartNewGame: expected 2 players, " +
                $"found {m_players.Count}.");

            return;
        }

        m_players[0].SetOponent(
            m_players[1].NetworkObjectId);

        m_players[1].SetOponent(
            m_players[0].NetworkObjectId);

        CreateDeck();

        m_gameCoroutine =
            StartCoroutine(GameLoop());

        Debug.Log(
            "GameMaster: new GameLoop started.");
    }
}
