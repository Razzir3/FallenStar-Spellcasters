using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Unity.Netcode;
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
    RoundEnd,
    GameOver
}

public enum MatchEndReason
{
    LivesDepleted,
    OpponentDisconnected,
    Surrendered
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
    [SerializeField] private float m_respawnWaitTime = 30f;
    [Header("Movement")]
    [SerializeField]
    private float m_stepMoveDistance = 3f;
    [Header("GameRules")]
    [SerializeField] private int m_startHandCards = 2;
    // 0 or less = no limit.
    [SerializeField] private int m_maxHandCards = 10;
    [SerializeField] private int m_baseMana = 2;
    [SerializeField] private int m_manaGrowthStep = 2;
    [SerializeField] private int m_maxMana = 20;
    [SerializeField] private CardRarity m_refillDeckMaxRarity = CardRarity.Rare;
    [SerializeField] private int m_maxSelectionTimerResetsPerPhase = 1;
    [SerializeField] private int m_maxSelectionTimerResetsPerMatch = 3;

    public NetworkVariable <GamePhase> m_gamePhase = new NetworkVariable<GamePhase>(GamePhase.None, NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> m_isInputLocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> m_selectionPhaseId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private List<PlayerSession> m_players = new();
    private readonly Dictionary<PlayerSession, PlayerTurnSelection> m_turnSelections = new();
    private readonly Dictionary<PlayerSession, int> m_selectionTimerResetsThisPhase = new();
    private readonly Dictionary<PlayerSession, int> m_selectionTimerResetsThisMatch = new();

    public NetworkVariable<NetworkBehaviourReference> m_activePlayer;
    public NetworkVariable<NetworkBehaviourReference> m_waitingRespawnPlayer;
    

    private bool m_isResolvingCards;
    private bool m_isWaitingRespawn;


    private List<PlayerSession> m_turnOrder = new();

    private bool m_activePlayerConfirmedAction;
    private bool m_respawnConfirmed;

    private ulong m_respawnBlockNetworkId = 0;

    private Coroutine m_gameCoroutine;

    private bool m_isAbortingMatch = false;
    private bool m_isMatchEnded = false;
    private const ulong k_NoWinnerClientId = ulong.MaxValue;

    private float m_phaseElapsed;
    private float m_phaseDuration;
    public NetworkVariable<double> m_phaseEndServerTime = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void StartPhaseCountdown(float duration, float alreadyElapsed = 0f)
    {
        m_phaseDuration = duration;
        m_phaseEndServerTime.Value = NetworkManager.ServerTime.Time + Mathf.Max(0f, duration - alreadyElapsed);
    }

    private void ClearPhaseCountdown()
    {
        m_phaseEndServerTime.Value = 0;
    }

    public int GetPhaseSecondsLeft()
    {
        if (m_phaseEndServerTime.Value <= 0 || NetworkManager == null)
            return 0;

        return Mathf.Max(0, Mathf.CeilToInt((float)(m_phaseEndServerTime.Value - NetworkManager.ServerTime.Time)));
    }

    private bool m_externalDeathDetected = false;

    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        m_debugMode = DebugModeGuard.Allow(m_debugMode);
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
        // Only the server handles input packages.
        if (!IsServer) return;

        bool _locked = m_isResolvingCards || m_isWaitingRespawn;
        if (m_isInputLocked.Value != _locked)
            m_isInputLocked.Value = _locked;

        InputQueueUpdate();
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
        if (IsServer && IsSpawned)
            m_isInputLocked.Value = false;
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
    public void EndMatch(PlayerSession winner, MatchEndReason reason)
    {
        if (!IsServer || m_isMatchEnded)
            return;

        m_isMatchEnded = true;
        ClearPhaseCountdown();
        m_gamePhase.Value = GamePhase.GameOver;
        m_activePlayer.Value = default;
        m_waitingRespawnPlayer.Value = default;
        m_inputQueue.Clear();
        m_cardInputQueue.Clear();

        ulong winnerClientId = winner != null ? winner.OwnerClientId : k_NoWinnerClientId;

        if (m_debugMode)
        {
            Debug.Log($"GameMaster: match ended. Winner: {winnerClientId}. Reason: {reason}");
            foreach (PlayerSession player in m_players)
            {
                if (player == null)
                    continue;

                SuspiciousActivityLog suspiciousLog = player.GetSuspiciousLog();
                if (suspiciousLog.Total > 0)
                {
                    Debug.Log($"[Suspicious] client {player.OwnerClientId} match summary: {suspiciousLog.ToSummaryString()}");
                }
            }
        }

        MatchEndedClientRpc(winnerClientId, reason);
    }

    [ClientRpc]
    private void MatchEndedClientRpc(ulong winnerClientId, MatchEndReason reason)
    {
        bool isDraw = winnerClientId == k_NoWinnerClientId;
        bool isWinner = !isDraw && winnerClientId == NetworkManager.Singleton.LocalClientId;
        MatchResultData.Set(isWinner, isDraw, reason);

        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.LeaveMatchToResultScene();
        }
    }

    public void AbortMatch(string reason)
    {
        if (!IsServer || m_isAbortingMatch)
            return;

        m_isAbortingMatch = true;
        ClearPhaseCountdown();
        m_gamePhase.Value = GamePhase.None;

        Debug.LogError("GameMaster: match aborted. Reason: " + reason);

        if (m_debugMode)
        {
            foreach (PlayerSession player in m_players)
            {
                if (player == null)
                    continue;

                SuspiciousActivityLog suspiciousLog = player.GetSuspiciousLog();
                if (suspiciousLog.Total > 0)
                {
                    Debug.Log($"[Suspicious] client {player.OwnerClientId} match summary: {suspiciousLog.ToSummaryString()}");
                }
            }
        }

        if (MultiplayerManager.Instance != null)
        {
            MultiplayerManager.Instance.Disconnect();
        }
    }

    public void StartNewGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "GameMaster.StartNewGame can only be called on server.");

            return;
        }

        m_isAbortingMatch = false;
        m_isMatchEnded = false;

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
        m_selectionTimerResetsThisMatch.Clear();

        m_inputQueue.Clear();
        m_cardInputQueue.Clear();

        m_deck.Clear();
        m_graveyard.Clear();

        m_isResolvingCards = false;
        m_isWaitingRespawn = false;
        if (IsServer && IsSpawned)
        {
            m_isInputLocked.Value = false;
            m_selectionPhaseId.Value = 0;
        }
        m_activePlayerConfirmedAction = false;
        m_respawnConfirmed = false;

        m_activePlayer.Value = default;
        m_waitingRespawnPlayer.Value = default;

        m_phaseElapsed = 0f;
        ClearPhaseCountdown();
        m_gamePhase.Value = GamePhase.None;

        if (SessionManager.Instance == null)
        {
            Debug.LogError(
                "GameMaster.StartNewGame: SessionManager.Instance is null.");

            AbortMatch("match_start_failed");
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

            AbortMatch("match_start_failed");
            return;
        }

        foreach (PlayerSession player in m_players)
        {
            player.m_manaCapacity.Value = Mathf.Clamp(m_baseMana, 0, m_maxMana);
            player.m_manaCurrent.Value = player.m_manaCapacity.Value;
        }

        m_players[0].SetOponent(
            m_players[1].NetworkObjectId);

        m_players[1].SetOponent(
            m_players[0].NetworkObjectId);

        CreateDeck();

        m_gameCoroutine =
            StartCoroutine(RunGuarded(GameLoop()));

        if (m_debugMode)
            Debug.Log(
                "GameMaster: new GameLoop started.");
    }
}
