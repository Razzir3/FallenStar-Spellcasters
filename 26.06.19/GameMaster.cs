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

public class GameMaster : NetworkBehaviour
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

        //test only
        
        if(Keyboard.current.lKey.wasPressedThisFrame)
        {
            NetworkCard card = Instantiate(m_cardPrefab);
            //card.m_cardData = m_cardDatabase.cards.Find(x => x.m_nameKey == "cardName_fireball");
            card.m_cardData = m_cardDatabase.m_cards[Random.Range(0, m_cardDatabase.m_cards.Count)];
            card.NetworkObject.Spawn();
            card.m_isFaceUp.Value = true;
            SessionManager.Instance.LocalPlayer.AddCard(card);
            m_spawnedCards.Add(card);
            card.SetOwner(SessionManager.Instance.LocalPlayer.NetworkObjectId);
        }
        if(Keyboard.current.kKey.wasPressedThisFrame)
        {
            var _player = SessionManager.Instance.LocalPlayer;
            var _playerCards = _player.m_handCards;
            if ((_playerCards.Count > 0))
            {
                _playerCards.RemoveAt(0);
            }
        }
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            //TestOnly
            NetworkCard card = Instantiate(m_cardPrefab);
            //card.m_cardData = m_cardDatabase.cards.Find(x => x.m_nameKey == "cardName_fireball");
            card.m_cardData = m_cardDatabase.m_cards[Random.Range(0, m_cardDatabase.m_cards.Count)];
            card.NetworkObject.Spawn();
            card.m_isFaceUp.Value = true;
            var _test = Instantiate(m_cardViewWorldPrefab, new Vector3(0, 0, -40), Quaternion.Euler(90f, 90f, 0f));
            var _test2 = _test.GetComponent<CardViewWorld>();
            _test2.Setup(card.m_cardData);
            _test2.SetFaceUp(true);
            _test2.SetNetworkCard(card);
            card.SetCurrentView(_test2);
        }
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            StartNewGame();
        }
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

    #region PackageManaging(Rpc)

    [ClientRpc]
    public void SendCardDataClientRpc(
    ulong cardNetworkObjectId,
    string cardId,
    ClientRpcParams clientRpcParams = default)
    {
        if (m_debugMode)
        {
            Debug.Log(
                $"SendCardDataClientRpc called. " +
                $"Card: {cardNetworkObjectId}, CardId: {cardId}");
        }

        NetworkCard _card =
            GetCardByID(cardNetworkObjectId);

        if (_card == null)
        {
            Debug.LogWarning(
                $"SendCardDataClientRpc: NetworkCard " +
                $"{cardNetworkObjectId} was not found.");

            return;
        }

        _card.RepairCardData(cardId);

        if (CardWorldViewManager.Instance != null)
        {
            CardWorldViewManager.Instance.RefreshAllSessions();
        }
    }
    public bool HandlePlayerControlPackage(PlayerControlPackage _package)
    {
        try
        {
            if (!IsHost)
            {
                if (m_debugMode) Debug.Log("ItsNotServer GM");
                return false;
            }

            m_inputQueue.Enqueue(_package);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return false;
        }

        if (m_debugMode) Debug.Log("Handle Package");
        return true;
    }

    public bool HandleCardControlPackage(
        CardControllPackage _package,
        ulong senderClientId)
    {
        try
        {
            if (!IsServer)
            {
                if (m_debugMode)
                {
                    Debug.Log(
                        "Its Not server GM");
                }

                return false;
            }

            if (m_gamePhase.Value !=
                GamePhase.PlayerAction)
            {
                Debug.LogWarning(
                    $"Card package rejected. " +
                    $"Current phase: {m_gamePhase.Value}");

                return false;
            }

            if (!m_activePlayer.Value.TryGet(
                    out PlayerSession _activePlayer))
            {
                Debug.LogWarning(
                    "Card package rejected. " +
                    "Active player was not found.");

                return false;
            }

            if (_activePlayer.OwnerClientId !=
                senderClientId)
            {
                Debug.LogWarning(
                    $"Card package rejected. " +
                    $"Sender {senderClientId} is not active player.");

                return false;
            }

            NetworkCard _card =
                GetCardByID(
                    _package.CardNetworkObjectId);

            if (_card == null)
            {
                Debug.LogWarning(
                    $"Card {_package.CardNetworkObjectId} " +
                    $"was not found.");

                return false;
            }

            if (_card.m_ownerId.Value !=
                _activePlayer.NetworkObjectId)
            {
                Debug.LogWarning(
                    $"Card package rejected. " +
                    $"Wrong card owner.");

                return false;
            }

            if (!ContainsNetworkObjectId(
                    _activePlayer.m_boardPlayCards,
                    _card.NetworkObjectId))
            {
                Debug.LogWarning(
                    $"Card package rejected. " +
                    $"Card is not present in m_boardPlayCards.");

                return false;
            }

            m_cardInputQueue.Enqueue(_package);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return false;
        }
    }
    private void InputQueueUpdate()
    {
        while (m_inputQueue.Count > 0)
        {
            PlayerControlPackage _package = m_inputQueue.Dequeue();
            //Package is valid, we can process it and send the result back to the client
            ToPlayerControlPackage _packageNew = new ToPlayerControlPackage
            {
                RequestId = _package.RequestId,
                ClientId = _package.ClientId,
                s_horizontal = _package.s_horizontal,
                s_vertical = _package.s_vertical,
                s_enabled = true
            };
            if (m_debugMode) Debug.Log("Handle Packege in Update");

            //We create the ClientRpcParams to specify which client should receive the result of the processed package
            ClientRpcParams _rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[]
                                    {
                                        _package.ClientId
                                    }
                }

            };
            //We get the NetworkClient of the client that sent the package to access its PlayerObject and apply the movement based on the input received
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(_package.ClientId))
            {
                if (m_debugMode)
                {
                    Debug.Log("Client disconnected: {_package.ClientId}");
                }

                continue;
            }
            NetworkClient _client = NetworkManager.Singleton.ConnectedClients[_package.ClientId];

            var _playerController = _client.PlayerObject.GetComponent<PlayerController>();

            if (_playerController != null)
            {

                MoveInputProcessing(_playerController, _package);
                HPDeltaProcessing(_playerController, _package);
            }

            //Send Package back to the client to confirm that the input was processed and to update the client's state if necessary
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(_package.ClientId))
            {
                continue;
            }
            SendToClientRpc(_packageNew, _rpcParams);
        }
    }
    //Todo
    //Rework?
    /*
    private void CardMessageQueueUpdate()
    {
        while (m_cardInputQueue.Count > 0)
        {
            Debug.Log("CardMessageQueue");
            var _currentMessage = m_cardInputQueue.Dequeue();

            NetworkCard _currentCard = GetCardByID(_currentMessage.CardNetworkObjectId);

            if (_currentCard == null)
            {
                Debug.LogError("Card dont founded");
                continue;
            }

            var _data = _currentCard.m_cardData;
            int _checkResultSum = 0;
            for (int i = 0; i < _currentMessage.TargetNetworkObjectId.Count; i++)
            {
                if (_currentMessage.TargetNetworkObjectId[i] != 0)
                {
                    if (CheckActivity(_data.m_activities[i], _currentMessage.TargetNetworkObjectId[i], _currentCard.m_ownerId.Value))
                    {
                        _checkResultSum++;
                    }
                }
                else
                {
                    _checkResultSum++;
                }
            }
            Debug.Log("Check sum " + _checkResultSum);
            if (_checkResultSum == _currentMessage.TargetNetworkObjectId.Count)
            {
                Debug.Log("Check");
                for (int i = 0; i < _currentMessage.TargetNetworkObjectId.Count; i++)
                {
                    if (_currentMessage.TargetNetworkObjectId[i] != 0)
                    {
                        Debug.Log("Check");
                        ProcessActivity(_data.m_activities[i], _currentMessage.TargetNetworkObjectId[i], _currentCard.m_ownerId.Value);
                    }
                }
            }
            else
            {
                Debug.LogWarning("SuspectActivity");
            }
            MoveCardOnGraveyard(_currentCard);
        }
    }
    */
    private bool CheckActivity(CardActivity activity, ulong targetID, ulong cardOwnerId)
    {
        bool _result = false;
        var _ownerPlayer = GetSessionByID(cardOwnerId);
        var _ownerPlayerOponent = GetSessionByID(_ownerPlayer.m_currentOponentNetworkObjectId.Value);

        if (_ownerPlayer == null)
        {
            Debug.LogError("No owner");
            return false;
        }
        PlayerController _targetCharacter = null;
        var _targetSession = GetSessionByID(targetID);
        if (_targetSession != null)
        {
            _targetCharacter = GetSessionByID(targetID).GetCharacter();
        }
        BlockController _targetBlock = GetBlockById(targetID);
            switch (activity.s_targetType)
            {
                //todo
                case CardTargetType.AllBlockAnyPlayer:
                case CardTargetType.AllPlayer:
                case CardTargetType.AllBlockSelf:
                case CardTargetType.AllBlockOpponent:
                case CardTargetType.RandomBlockSelectedPlayer:
                case CardTargetType.RandomOpponentBlock:
                case CardTargetType.RandomSelfBlock:
                    _result = true;
                    break;
                case CardTargetType.AnyPlayer:

                    if (_targetCharacter != null) _result = true;
                    break;
                case CardTargetType.AnyPlayerBlock:
                    if (_targetBlock != null) _result = true;
                    break;
                case CardTargetType.OpponentBlock:
                    if (_targetBlock != null)
                    {
                        if (_targetBlock.m_teamId.Value == _ownerPlayerOponent.TeamId.Value)
                        {
                              _result = true;
                         }
                        else break;
                    }
                    _result = true;
                    break;
                case CardTargetType.OpponentPlayer:
                    if(_ownerPlayerOponent.GetCharacter()==_targetCharacter)
                        {
                            _result = true;
                        }
                else
                {
                    Debug.Log("Error");
                }
                    break;
                case CardTargetType.SelfBlock:
                    if (_targetBlock != null)
                    {
                        if (_targetBlock.m_teamId.Value == _ownerPlayer.TeamId.Value)
                        {
                            _result = true;
                        }
                        else break;
                    }
                    _result = true;
                    break;
                case CardTargetType.SelfPlayer:
                    if(_ownerPlayer.GetCharacter()==_targetCharacter)
                    {
                        _result = true;
                    }
                    break;
                case CardTargetType.None:
                    _result = true;
                    break;
            }

        return _result;
    }
    //todo for all
    private void ProcessActivity(CardActivity activity, ulong targetID, ulong cardOwnerId)
    {
        Debug.Log("Process called");
        PlayerController _ownerPlayer = GetSessionByID(cardOwnerId).GetCharacter();
        PlayerController _targetCharacter = null;
        var _targetSession = GetSessionByID(targetID);
        if (_targetSession != null)
        {
            _targetCharacter = GetSessionByID(targetID).GetCharacter();
        }
        BlockController _targetBlock = GetBlockById(targetID);
        switch (activity.s_actionType)
        {
           case CardActionType.None:
                break;
           case CardActionType.Damage:
                if(_targetCharacter != null)
                if(activity.s_targetType == CardTargetType.AnyPlayer|| activity.s_targetType == CardTargetType.OpponentPlayer|| activity.s_targetType == CardTargetType.SelfPlayer)
                {
                    _targetCharacter.m_hpCurrent.Value -= activity.s_value;
                    UpdatePlayerHP(_targetCharacter);
                }
                    else
                    {
                        Debug.LogWarning("Error here");
                    }

                if (_targetBlock != null)
                        if (activity.s_targetType == CardTargetType.AnyPlayerBlock || activity.s_targetType == CardTargetType.OpponentBlock || activity.s_targetType == CardTargetType.SelfBlock)
                        {
                            _targetBlock.m_blockHP.Value -= activity.s_value;
                            UpdateBlockHp(_targetBlock);
                        }
                break;
            case CardActionType.Heal:
                if(_targetCharacter != null)
                if (activity.s_targetType == CardTargetType.AnyPlayer || activity.s_targetType == CardTargetType.OpponentPlayer || activity.s_targetType == CardTargetType.SelfPlayer)
                {
                    if (_targetCharacter.m_hpCurrent.Value <= (_targetCharacter.m_hpMax.Value - activity.s_value))
                    {
                        _targetCharacter.m_hpCurrent.Value += activity.s_value;
                    }
                    else
                    {
                        double _shieldDelta = (activity.s_value - _targetCharacter.m_hpMax.Value + _targetCharacter.m_hpCurrent.Value)*0.5;
                        _targetCharacter.m_hpCurrent.Value = _targetCharacter.m_hpMax.Value + (_shieldDelta).ConvertTo<int>() ;
                    }
                    UpdatePlayerHP(_targetCharacter);
                }
                //todo
                if(_targetBlock != null) 
                if (activity.s_targetType == CardTargetType.AnyPlayerBlock || activity.s_targetType == CardTargetType.OpponentBlock || activity.s_targetType == CardTargetType.SelfBlock)
                {

                    if (_targetBlock.m_blockHP.Value <= (100 - activity.s_value))
                    {
                        _targetBlock.m_blockHP.Value += activity.s_value;
                    }
                    else
                    {
                        double _shieldDelta = (activity.s_value - 100 + _targetBlock.m_blockHP.Value) * 0.5;
                        _targetBlock.m_blockHP.Value = _targetCharacter.m_hpMax.Value + (_shieldDelta).ConvertTo<int>();
                    }
                    UpdateBlockHp(_targetBlock);
                }
                break;
            case CardActionType.DrawCard:
                for (int i = 0; i < activity.s_value; i++)
                {
                    DrawCard(_targetSession);
                }
                break;
                //todo
            case CardActionType.DiscardCardHeal:
            case CardActionType.DiscardCard:
            case CardActionType.DiscardCardDamage:
            case CardActionType.DiscardCardDrawCard:
            case CardActionType.DrawCardDamage:
            case CardActionType.DrawCardDiscardCard:
            case CardActionType.DrawCardHeal:
            case CardActionType.DrawCardDrawCard:
            case CardActionType.DiscardCardDiscardCard:
                break;
        }
    }
    private void MoveInputProcessing(PlayerController controller, PlayerControlPackage _package)
    {
        Transform _playerTransform = controller.transform;

        Vector3 _direction = new Vector3(_package.s_horizontal,
                                        0,
                                        _package.s_vertical);
        var _playerSpeedCurrent = controller.m_moveSpeedCurrent.Value;

        _playerTransform.position += _direction * _playerSpeedCurrent;
    }

    private void HPDeltaProcessing(PlayerController controller, PlayerControlPackage _package)
    {
        if (_package.s_hpDelta != 0)
        {
            if (m_debugMode) Debug.Log($"CurrentHP before delta: {controller.GetCurrentHP()}");
            controller.m_hpCurrent.Value += _package.s_hpDelta;
            if (m_debugMode) Debug.Log($"HPDelta: {_package.s_hpDelta}");
            {
                UpdatePlayerHP(controller);
            }
        }
    }
    private void UpdatePlayerHP(PlayerController controller)
    {
        if (controller.m_hpCurrent.Value <= 0)
        {
            var _session = controller.GetSession();
            _session.m_lifeCurrent.Value -= 1;

            DespawnCharacter(_session);

            _session.GetSpawnPoint().IsOccupied = false;

            _session.SetCharacter(null);
            _session.CharacterObjectId.Value = 0;
        }
    }
    private void UpdateBlockHp(BlockController controller)
    {
        if(controller.m_blockHP.Value <=0)
        {
            controller.NetworkObject.Despawn(true);
        }
    }
    [ClientRpc]
    private void SendToClientRpc(ToPlayerControlPackage package, ClientRpcParams clientRpcParams = default)
    {
        if (m_debugMode) Debug.Log("SendToClientRpc");
        //PlayerController _player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();

        //_player.ReceiveValidatedInput(package);
    }
    //todo
    private void HandleConfirmSelectionTurn(PlayerSession session, ulong[] playCardNetworkId, ulong[] discardCardNetworkId)
    {
        
        if(!m_turnSelections.TryGetValue(session, out var turnSelections))
        {
            Debug.LogWarning("No selection found for session");
            return;
        }

        if(turnSelections.m_isConfirmed)
        {
            if (m_debugMode) Debug.Log("Selection already confirmed");
            return;
        }

        turnSelections.m_cardsToPlay.Clear();
        turnSelections.m_cardToDiscard.Clear();

        foreach( ulong cardId in playCardNetworkId)
        {
            NetworkCard _card =GetCardByID(cardId);
            if (_card == null) continue;

            if(ValidateCardOwner(_card,session))
            {
                turnSelections.m_cardsToPlay.Add(_card);
            }
        }
        foreach( ulong cardId in discardCardNetworkId)
        {
            NetworkCard _card =GetCardByID(cardId);
            if (_card == null) continue;

            if(ValidateCardOwner(_card,session))
            {
                turnSelections.m_cardToDiscard.Add(_card);
            }
        }

        turnSelections.m_isConfirmed = true;

        if(m_debugMode)
        {
            Debug.Log($"Player {session.OwnerClientId} confirmed card selection. Cost: {turnSelections.TotalCost()}");
        }
    }

    public void HandlePlayerActionConfirm(PlayerSession player)
    {
        if (player != m_activePlayer.Value)
        {
            Debug.LogWarning("Only active player can confirm action phase.");
            return;
        }
        m_activePlayerConfirmedAction = true;
    }

    private void HandlePlayerRespawnPoint(PlayerSession player, ulong networkObjectId)
    {
        if (player == m_waitingRespawnPlayer.Value)
        {
            m_respawnBlockNetworkId = networkObjectId;
            player.SetSpawnPoint(GetBlockById(networkObjectId).GetSpawn());
            RespawnCharacter(player);
        }
    }
    //todo
    /*
    public bool HandleCardViewWorld(ulong cardNetworkId, ulong senderClientId, CardViewType type)
    {
        if(SessionManager.Instance.m_playerSessions.ContainsKey(senderClientId))
        {
            var _session = SessionManager.Instance.m_playerSessions[senderClientId];
            var _card = GetCardByID(cardNetworkId);

            if(_card == null || _session == null)
            {
                return false;
            }

            if(_card.m_ownerId.Value != _session.NetworkObjectId)
            {
                Debug.LogError("Wrong owner. Suspect");
                return false;
            }

            
        }
    }
    */
    #endregion
    private bool ValidateCardOwner(NetworkCard card, PlayerSession session)
    {
        return (session.TeamId.Value == GetSessionByID(card.m_ownerId.Value).TeamId.Value);
    }

    public CardDatabase GetCardDatabase() { return m_cardDatabase; }

    private void BuildTurnOrderByCardCost()
    {
        m_turnOrder.Clear();

        List<PlayerSession> _turnOrder = m_turnSelections.OrderByDescending(kvp => kvp.Value.TotalCost()).Select(kvp => kvp.Key).ToList();
        m_turnOrder = _turnOrder;
    }
    #region SpawningFunc

    #region PlayerController SpawnFunc
    private void SpawnCharacter(PlayerSession session)
    {
        if (m_debugMode)
        {
            Debug.Log("Attempting to spawn character for client: " + session.ClientId.Value);
        }

        if (!IsServer)
        {
            return;
        }

        if (!SessionManager.Instance.m_playerSessions.ContainsKey(session.ClientId.Value))
        {
            Debug.LogError(session.ClientId.Value + " does not have a session.");
            return;
        }

        if (session.GetCharacter() != null)
        {
            Debug.LogError(session.ClientId.Value + " already has a character.");
            return;
        }

        SpawnPoint _spawnPoint = GetSpawnPoint(session.TeamId.Value);

        if (_spawnPoint == null)
        {
            Debug.LogError("No spawn point available for team: " + session.TeamId.Value);
            return;
        }

        GameObject _characterObject = Instantiate(m_playerCharacterPrefab, _spawnPoint.transform.position, _spawnPoint.transform.rotation);

        NetworkObject _networkObject = _characterObject.GetComponent<NetworkObject>();

        if (_networkObject != null)
        {
            PlayerController _playerController = _characterObject.GetComponent<PlayerController>();

            if (_playerController == null)
            {
                Debug.LogError("Player prefab dont have any player controller");
                return;
            }

            _networkObject.SpawnAsPlayerObject(session.ClientId.Value);

            _playerController.SessionObjectId.Value = session.NetworkObjectId;

            _playerController.Initialize(session);

            session.CharacterObjectId.Value = _networkObject.NetworkObjectId;

            session.SetSpawnPoint(_spawnPoint);

            session.SetCharacter(_playerController);

            session.MovePlayeToSpawnPoint();

            _spawnPoint.IsOccupied = true;

            if ((m_debugMode))
            {
                Debug.Log("Character spawned for client: " + session.ClientId.Value + " at spawn point: " + _spawnPoint.name);
            }
        }
        else
        {
            Debug.LogError("Player character prefab does not have a NetworkObject component.");
        }
    }

    //ToDo
    private void RespawnCharacter(PlayerSession session)
    {
        if (m_debugMode)
        {
            if (m_debugMode) Debug.Log("Attempting to respawn character for client: " + session.ClientId);
        }

        if (!IsServer)
        {
            return;
        }

        if (!SessionManager.Instance.m_playerSessions.ContainsKey(session.ClientId.Value))
        {
            Debug.LogError(session.ClientId + " does not have a session.");
            return;
        }

        if (session.m_lifeCurrent.Value < 0)
        {
            return;
        }

        if (session.GetCharacter() != null)
        {
            Debug.LogError(session.ClientId + " already has a character.");
            return;
        }
        var _spawnPoint = GetSpawnPoint(session.TeamId.Value);
        GameObject _characterObject = Instantiate(m_playerCharacterPrefab, _spawnPoint.transform.position, _spawnPoint.transform.rotation);

        NetworkObject _networkObject = _characterObject.GetComponent<NetworkObject>();

        if (_networkObject != null)
        {
            PlayerController _playerController = _characterObject.GetComponent<PlayerController>();

            if (_playerController == null)
            {
                Debug.LogError("Player prefab dont have any player controller");
                return;
            }

            _networkObject.SpawnAsPlayerObject(session.ClientId.Value);

            _playerController.SessionObjectId.Value = session.NetworkObjectId;

            _playerController.Initialize(session);

            session.CharacterObjectId.Value = _networkObject.NetworkObjectId;

            session.SetSpawnPoint(_spawnPoint);

            session.SetCharacter(_playerController);
            //todo
            session.MovePlayeToSpawnPoint(2.5f);

            _spawnPoint.IsOccupied = true;

            if ((m_debugMode))
            {
                Debug.Log("Character spawned for client: " + session.ClientId + " at spawn point: " + _spawnPoint.name);
            }
        }
        else
        {
            Debug.LogError("Player character prefab does not have a NetworkObject component.");
        }
    }

    private void DespawnCharacter(PlayerSession session)
    {
        if (!IsServer)
        {
            return;
        }

        if (!SessionManager.Instance.m_playerSessions.ContainsKey(session.ClientId.Value))
        {
            Debug.LogError(session.ClientId.Value + " does not have a session.");
            return;
        }

        PlayerController _playerController = session.GetCharacter();

        if (_playerController == null)
        {
            Debug.LogError(session.ClientId.Value + " does not have a character to despawn.");
            return;
        }

        SpawnPoint _spawnPoint = _playerController.GetSession().GetSpawnPoint();

        if (_spawnPoint != null)
        {
            _spawnPoint.IsOccupied = false;
        }

        session.SetCharacter(null);

        _playerController.NetworkObject.Despawn(true);

        if (m_debugMode)
        {
            Debug.Log("Character despawned for client: " + session.ClientId.Value);
        }
    }

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
    #endregion

    #region BlockSpawnFunc
    private void CreateBlock(PlayerSession session)
    {
        if (!IsServer)
        {
            return;
        }

        var _spawnPoint = GetSpawnPoint(session.TeamId.Value);
        if (_spawnPoint == null)
        {
            Debug.LogError("No Spawn Point");
        }
        for(int i = 0;i<5;i++)
        {
            for (int j = 0;j<5;j++)
            {
                Vector3 _spawnPosition =
                _spawnPoint.transform.position + Vector3.down * 2.5f+Vector3.left*(-2+j)*3f+Vector3.forward*(2-i)*3f;
                GameObject _obj = Instantiate(m_blockPrefab, _spawnPosition, _spawnPoint.gameObject.transform.rotation);

                NetworkObject _netObj = _obj.GetComponent<NetworkObject>();
                if (_netObj == null)
                {
                    Debug.LogError("No network object");
                    return;
                }
                //todo
                //BlockController _blockController = _netObj.GetComponentInChildren<BlockController>();
                BlockController _blockController = _netObj.GetComponent<BlockController>();
                if (_blockController == null)
                {
                    Debug.LogError("Prefab is not a block");
                    return;
                }
                _netObj.SpawnWithOwnership(session.ClientId.Value, true);
                _blockController.Initialize(session);
                session.AddBlock(_blockController,i,j);
            }
        }
        
    }

    private void ClearAllBlock(PlayerSession session)
    {
        if (!IsServer)
        {
            return;
        }
        session.ClearBlocks();
    }
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

    private NetworkCard GetCardByID(ulong networkObjectId)
    {
        NetworkCard _result = null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject _networkObject))
        {
            try
            {
                _result = _networkObject.GetComponent<NetworkCard>();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        return _result;
    }
    public void SpawnPlayers()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (var _playerSession in
                 SessionManager.Instance.m_playerSessions)
        {
            switch (_playerSession.Value.GetSessionRole())
            {
                case SessionRole.Player:
                    CreateBlock(_playerSession.Value);
                    SpawnCharacter(_playerSession.Value);
                    break;

                case SessionRole.Spectator:
                    break;
            }
        }

        BindPlayerCardWorldAreas();

        MultiplayerManager.Instance.m_currentMultiplayerStatus =
            MultiplayerStatus.InGameStatus;
    }
    #endregion

    #region SpawningPointsFunc
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


    //todo
    public SpawnPoint[] GetRespawnPoints(PlayerSession session)
    {
        var _sessionBlocks = session.GetSessionBlock();
        var _sessionRespawnPoints = new SpawnPoint[_sessionBlocks.Length];
        int i = 0;
        foreach (var _sessionBlock in _sessionBlocks)
        {
            _sessionRespawnPoints[i] = _sessionBlock.GetSpawn();
            i++;
        }
        return _sessionRespawnPoints;
    }
    #endregion
    #region PlayerCardWorldArea Func

    private void FindPlayerCardWorldAreas()
    {
        m_playerCardWorldAreas =
            FindObjectsByType<PlayerCardWorldArea>(
                FindObjectsSortMode.None);

        if (m_playerCardWorldAreas == null ||
            m_playerCardWorldAreas.Length == 0)
        {
            Debug.LogWarning(
                "PlayerCardWorldArea objects were not found on the scene.");

            return;
        }

        if (m_debugMode)
        {
            Debug.Log(
                $"Found PlayerCardWorldArea objects: " +
                $"{m_playerCardWorldAreas.Length}");
        }
    }

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
            Debug.LogWarning(
                "Cannot bind PlayerCardWorldArea: no areas found.");

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

            PlayerSession foundedSession =
                FindSessionForCardWorldArea(area.m_sessionSearchId);

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

    private PlayerSession FindSessionForCardWorldArea(
        int sessionSearchId)
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

    public PlayerCardWorldArea GetPlayerCardWorldArea(
        PlayerSession playerSession)
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
    public PlayerCardWorldArea GetPlayerCardWorldArea(
        ulong playerSessionNetworkObjectId)
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

    #endregion

    #region ClearFunc
    public void ClearupAfterDisconnect()
    {
        DespawnAllCards();

        m_inputQueue.Clear();

        foreach (var _spawnPoint in m_spawnPoint)
        {
            _spawnPoint.IsOccupied = false;
        }

        foreach (var _playerSession in
                 SessionManager.Instance.m_playerSessions)
        {
            _playerSession.Value.ClearSession();
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

    private void MoveCardOnGraveyard(NetworkCard card)
    {
        if (!IsServer)
            return;

        if (card == null)
            return;

        ulong _cardNetworkObjectId =
            card.NetworkObjectId;

        PlayerSession _owner =
            GetSessionByID(card.m_ownerId.Value);

        if (_owner != null)
        {
            RemoveNetworkObjectId(
                _owner.m_handCards,
                _cardNetworkObjectId);

            RemoveNetworkObjectId(
                _owner.m_boardPlayCards,
                _cardNetworkObjectId);

            RemoveNetworkObjectId(
                _owner.m_boardDiscardCards,
                _cardNetworkObjectId);
        }
        else
        {
            Debug.LogWarning(
                $"Owner was not found for card " +
                $"{_cardNetworkObjectId}. Owner ID: " +
                $"{card.m_ownerId.Value}");
        }

        card.SetLocation(
            CardLocation.Graveyard);

        if (!m_graveyard.Contains(card))
        {
            m_graveyard.Add(card);
        }

        if (m_debugMode)
        {
            Debug.Log(
                $"Card {_cardNetworkObjectId} moved to graveyard.");
        }
    }
    private bool ContainsNetworkObjectId(
    NetworkList<ulong> networkList,
    ulong networkObjectId)
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
    private void RemoveNetworkObjectId(
    NetworkList<ulong> networkList,
    ulong networkObjectId)
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
    private void ClearDiscardCards(PlayerSession player)
    {
        if (!IsServer)
            return;

        if (player == null)
            return;

        List<NetworkCard> _cardsToRemove =
            new List<NetworkCard>();

        for (int i = 0;
             i < player.m_boardDiscardCards.Count;
             i++)
        {
            ulong _cardNetworkObjectId =
                player.m_boardDiscardCards[i];

            NetworkCard _card =
                GetCardByID(_cardNetworkObjectId);

            if (_card != null)
            {
                _cardsToRemove.Add(_card);
            }
        }

        foreach (NetworkCard _card in _cardsToRemove)
        {
            MoveCardOnGraveyard(_card);
        }

        player.m_boardDiscardCards.Clear();
    }
    private void ClearPlayCards(PlayerSession player)
    {
        if (!IsServer)
            return;

        if (player == null)
            return;

        List<NetworkCard> _cardsToRemove =
            new List<NetworkCard>();

        for (int i = 0;
             i < player.m_boardPlayCards.Count;
             i++)
        {
            ulong _cardNetworkObjectId =
                player.m_boardPlayCards[i];

            NetworkCard _card =
                GetCardByID(_cardNetworkObjectId);

            if (_card != null)
            {
                _cardsToRemove.Add(_card);
            }
        }

        foreach (NetworkCard _card in _cardsToRemove)
        {
            MoveCardOnGraveyard(_card);
        }

        player.m_boardPlayCards.Clear();
    }
    #region GameStateCheckFunc
    private bool IsAnyPlayerDead()
    {
        bool _result = false;
        foreach (PlayerSession p in m_players)
        {
            if ((p.GetCharacter() == null) && (p.TeamId.Value != -1))
            {
                _result = true;
            }
        }
        return _result;
    }
    private bool IsGameFinished()
    {
        bool _result = false;
        List<PlayerSession> _players = new List<PlayerSession>();
        foreach (PlayerSession p in m_players)
        {
            if (p.TeamId.Value != -1)
            {
                _players.Add(p);
            }
        }
        int _playerAliveOrCanRespawn = 0;
        {
            foreach (PlayerSession p in _players)
            {
                var _char = p.GetCharacter();
                if (_char != null)
                {
                    _playerAliveOrCanRespawn++;
                    continue;
                }

                if (p.m_lifeCurrent.Value > 0)
                {
                    _playerAliveOrCanRespawn++;
                    continue;
                }
            }
        }
        if (_playerAliveOrCanRespawn <= 1)
        {
            _result = true;
        }
        return _result;
    }
    private PlayerSession FindDeadPlayer()
    {
        PlayerSession _result = null;
        List<PlayerSession> _players = new List<PlayerSession>();
        foreach (PlayerSession p in m_players)
        {
            if (p.TeamId.Value != -1)
            {
                _players.Add(p);
            }
        }
        for (int i = 0; i < _players.Count && _result == null; i++)
        {
            if (_players[i].GetCharacter() == null && _players[i].m_lifeCurrent.Value > 0)
            {
                _result = _players[i];
            }
        }
        return _result;
    }
    public void HandleConfirmTurn(ulong senderClientId, PlayerSession playerSession,
                                  ulong[] playCardNetworkIds, ulong[] discardCardNetworkIds, ulong respawnBlockNetworkId)
    {
        if (!IsServer)
        {
            return;
        }
        if (playerSession == null)
        {
            Debug.LogWarning("Confirm rejected. No player session");
            return;
        }
        if (playerSession.OwnerClientId != senderClientId)
        {
            Debug.LogWarning("Suspect Activity");
            return;
        }

        switch (m_gamePhase.Value)
        {
            case GamePhase.CardSelection:
                {
                    HandleConfirmSelectionTurn(
                        playerSession,
                        playCardNetworkIds,
                        discardCardNetworkIds);

                    playerSession.m_boardPlayCards.Clear();
                    playerSession.m_boardDiscardCards.Clear();

                    foreach (ulong cardNetworkObjectId
                             in playCardNetworkIds)
                    {
                        NetworkCard card =
                            GetCardByID(cardNetworkObjectId);

                        if (card == null)
                            continue;

                        if (card.m_cardData == null)
                        {
                            Debug.LogWarning(
                                $"CardData is null for play card " +
                                $"{cardNetworkObjectId}.");

                            continue;
                        }

                        card.SetLocation(CardLocation.Board);
                        card.m_isRotated.Value = false;

                        SendCardDataClientRpc(
                            card.NetworkObjectId,
                            card.m_cardData.m_cardId);

                        playerSession.m_boardPlayCards.Add(
                            cardNetworkObjectId);
                    }

                    foreach (ulong cardNetworkObjectId
                             in discardCardNetworkIds)
                    {
                        NetworkCard card =
                            GetCardByID(cardNetworkObjectId);

                        if (card == null)
                            continue;

                        if (card.m_cardData == null)
                        {
                            Debug.LogWarning(
                                $"CardData is null for discard card " +
                                $"{cardNetworkObjectId}.");

                            continue;
                        }

                        card.SetLocation(CardLocation.Board);
                        card.m_isRotated.Value = true;

                        SendCardDataClientRpc(
                            card.NetworkObjectId,
                            card.m_cardData.m_cardId);

                        playerSession.m_boardDiscardCards.Add(
                            cardNetworkObjectId);
                    }

                    break;
                }
            case GamePhase.PlayerAction:
            case GamePhase.ResolveCards:
                HandlePlayerActionConfirm(playerSession);
                break;
            case GamePhase.RespawnWait:
                HandlePlayerRespawnPoint(playerSession, respawnBlockNetworkId);
                break;
            default:
                if (m_debugMode) Debug.Log("Confirm ignored. Current phase{ }");
                break;
        }
                
    }

    private bool AllPlayerConfirmedSelection()
    {
        bool _result = false;
        int _sum = 0;
        foreach (var selection in m_turnSelections)
        {
            if (selection.Value.m_isConfirmed) _sum++;
        }
        if (_sum == m_turnSelections.Count)
        {
            _result = true;
        }
        return _result;
    }
    #endregion
    #region GameLoopFunc
    #region BlockTurn

    public bool CanPlayerMove (PlayerSession session)
    {
        return m_gamePhase.Value == GamePhase.PlayerAction && m_activePlayer.Value.TryGet(out PlayerSession player) && player == session
            && !m_isResolvingCards && !m_isWaitingRespawn;
    }

    public bool CanPlayerSelectCardTargets(PlayerSession session)
    {
        return m_gamePhase.Value == GamePhase.PlayerAction && m_activePlayer.Value.TryGet(out PlayerSession player)&&player == session
            &&!m_isResolvingCards&& !m_isWaitingRespawn;
    }

    public bool CanPlayerSelectCards(PlayerSession session)
    {
        return m_gamePhase.Value == GamePhase.CardSelection
            && !m_isResolvingCards && !m_isWaitingRespawn;
    }

    #endregion

    private IEnumerator PlayerTurnPhase()
    {
        foreach (PlayerSession player in m_turnOrder)
        {
            if (player == null)
                continue;

            m_activePlayer.Value = player;

            yield return PlayerActionPhase(player);

            if (IsGameFinished())
            {
                yield break;
            }
        }

        m_activePlayer.Value = null;
    }
    private IEnumerator PlayerActionPhase(PlayerSession player)
    {
        if (player == null)
            yield break;

        m_gamePhase.Value = GamePhase.PlayerAction;
        m_activePlayer.Value = player;

        m_activePlayerConfirmedAction = false;
        m_isResolvingCards = false;

        if (m_debugMode)
        {
            Debug.Log(
                $"Player Action phase started. " +
                $"Active player: {player.OwnerClientId}");
        }

        m_timer.Value = 0f;

        while (m_timer.Value < m_playerActionTime)
        {
            while (m_cardInputQueue.Count > 0)
            {
                CardControllPackage _currentMessage =
                    m_cardInputQueue.Dequeue();

                NetworkCard _currentCard =
                    GetCardByID(
                        _currentMessage.CardNetworkObjectId);

                if (_currentCard == null)
                {
                    Debug.LogWarning(
                        $"NetworkCard " +
                        $"{_currentMessage.CardNetworkObjectId} " +
                        $"was not found.");

                    continue;
                }

                if (_currentCard.m_ownerId.Value !=
                    player.NetworkObjectId)
                {
                    Debug.LogWarning(
                        $"Player {player.NetworkObjectId} tried to use " +
                        $"card {_currentCard.NetworkObjectId} owned by " +
                        $"{_currentCard.m_ownerId.Value}.");

                    continue;
                }

                if (!ContainsNetworkObjectId(
                        player.m_boardPlayCards,
                        _currentCard.NetworkObjectId))
                {
                    Debug.LogWarning(
                        $"Card {_currentCard.NetworkObjectId} " +
                        $"is not present in m_boardPlayCards.");

                    continue;
                }

                if (_currentCard.m_cardData == null)
                {
                    Debug.LogWarning(
                        $"CardData is null for card " +
                        $"{_currentCard.NetworkObjectId}.");

                    MoveCardOnGraveyard(_currentCard);
                    continue;
                }

                if (_currentCard.m_cardData.m_activities == null)
                {
                    Debug.LogWarning(
                        $"Activities are null for card " +
                        $"{_currentCard.NetworkObjectId}.");

                    MoveCardOnGraveyard(_currentCard);
                    continue;
                }

                m_isResolvingCards = true;

                bool _isMessageValid = true;

                int _activityCount =
                    _currentCard.m_cardData.m_activities.Length;

                int _targetCount =
                    _currentMessage.TargetNetworkObjectId.Count;

                if (_targetCount < _activityCount)
                {
                    Debug.LogWarning(
                        $"Card package contains {_targetCount} targets, " +
                        $"but card requires {_activityCount} activities.");

                    _isMessageValid = false;
                }

                if (_isMessageValid)
                {
                    for (int i = 0; i < _activityCount; i++)
                    {
                        CardActivity _activity =
                            _currentCard.m_cardData.m_activities[i];

                        ulong _targetNetworkObjectId =
                            _currentMessage.GetTarget(i);

                        if (_targetNetworkObjectId == 0)
                        {
                            switch (_activity.s_targetType)
                            {
                                case CardTargetType.None:
                                case CardTargetType.AllPlayer:
                                case CardTargetType.AllBlockAnyPlayer:
                                case CardTargetType.AllBlockSelf:
                                case CardTargetType.AllBlockOpponent:
                                case CardTargetType.RandomBlockSelectedPlayer:
                                case CardTargetType.RandomOpponentBlock:
                                case CardTargetType.RandomSelfBlock:
                                    continue;

                                default:
                                    Debug.LogWarning(
                                        $"Target {i} was not selected for " +
                                        $"card {_currentCard.NetworkObjectId}.");

                                    _isMessageValid = false;
                                    break;
                            }
                        }

                        if (!_isMessageValid)
                            break;

                        if (!CheckActivity(
                                _activity,
                                _targetNetworkObjectId,
                                _currentCard.m_ownerId.Value))
                        {
                            Debug.LogWarning(
                                $"Target validation failed. " +
                                $"Card: {_currentCard.NetworkObjectId}, " +
                                $"Activity index: {i}, " +
                                $"Target: {_targetNetworkObjectId}");

                            _isMessageValid = false;
                            break;
                        }
                    }
                }

                if (!_isMessageValid)
                {
                    m_isResolvingCards = false;
                    continue;
                }

                for (int i = 0; i < _activityCount; i++)
                {
                    CardActivity _activity =
                        _currentCard.m_cardData.m_activities[i];

                    ulong _targetNetworkObjectId =
                        _currentMessage.GetTarget(i);

                    ProcessActivity(
                        _activity,
                        _targetNetworkObjectId,
                        _currentCard.m_ownerId.Value);

                    if (IsAnyPlayerDead())
                    {
                        if (IsGameFinished())
                        {
                            break;
                        }

                        yield return WaitForRespawnIfNeeded();

                        if (!IsGameFinished())
                        {
                            m_gamePhase.Value =
                                GamePhase.PlayerAction;

                            m_activePlayer.Value = player;
                        }
                    }
                }

                MoveCardOnGraveyard(_currentCard);

                m_isResolvingCards = false;

                yield return null;
            }

            if (m_activePlayerConfirmedAction &&
                m_cardInputQueue.Count == 0 &&
                !m_isResolvingCards)
            {
                break;
            }

            m_timer.Value += Time.deltaTime;

            yield return null;
        }

        m_isResolvingCards = false;

        ClearDiscardCards(player);
        ClearPlayCards(player);

        if (m_debugMode)
        {
            Debug.Log(
                $"Player Action phase ended. " +
                $"Active player: {player.OwnerClientId}");
        }
    }
    private IEnumerator RoundBreakPhase()
    {
        m_gamePhase.Value = GamePhase.RoundBreak;

        if(m_debugMode)
        {
            Debug.Log("Round break phase started");
        }
        m_timer.Value = 0f;

        while (m_timer.Value < m_roundBreakTime)
        {
            m_timer.Value += Time.deltaTime;
            yield return null;
        }
        if (m_debugMode)
        {
            Debug.Log("Round break phase ended");
        }
    }

    //ToDo
    private IEnumerator ResolvePlayerCardsPhase(PlayerSession player)
    {
        m_gamePhase.Value = GamePhase.ResolveCards;
        m_isResolvingCards = true;

        if(!m_turnSelections.TryGetValue(player, out PlayerTurnSelection selection))
        {
            m_isResolvingCards = false;
            yield break;
        }

        while (m_cardInputQueue.Count > 0)
        {
            var _currentMessage = m_cardInputQueue.Dequeue();

            NetworkCard _currentCard = GetCardByID(_currentMessage.CardNetworkObjectId);

            if (_currentCard == null)
            {
                Debug.LogError("Card dont founded");
                continue;
            }

            var _data = _currentCard.m_cardData;
            int _checkResultSum = 0;
            for (int i = 0; i < _currentMessage.TargetNetworkObjectId.Count; i++)
            {
                if (_currentMessage.TargetNetworkObjectId[i] != 0)
                {
                    if (CheckActivity(_data.m_activities[i], _currentMessage.TargetNetworkObjectId[i], _currentCard.m_ownerId.Value))
                    {
                        _checkResultSum++;
                    }
                }
                else
                {
                    _checkResultSum++;
                }
            }
            if (_checkResultSum == _currentMessage.TargetNetworkObjectId.Count)
            {
                for (int i = 0; i < _currentMessage.TargetNetworkObjectId.Count; i++)
                {
                    if (_currentMessage.TargetNetworkObjectId[i] != 0)
                    {
                        ProcessActivity(_data.m_activities[i], _currentMessage.TargetNetworkObjectId[i], _currentCard.m_ownerId.Value);
                        if(IsAnyPlayerDead())
                        {
                            if (IsGameFinished())
                            {
                                break;
                            }
                            else
                            {
                                yield return WaitForRespawnIfNeeded();
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("SuspectActivity");
            }
            MoveCardOnGraveyard(_currentCard);
        }
    }

    
   
    private IEnumerator WaitForRespawnIfNeeded()
    {
        PlayerSession _deadPlayer;
        while (IsAnyPlayerDead())
        {
            _deadPlayer = FindDeadPlayer();
            if(_deadPlayer == null)
            {
                yield break;
            }

            m_gamePhase.Value = GamePhase.RespawnWait;
            m_isWaitingRespawn = true;
            m_waitingRespawnPlayer.Value = _deadPlayer;
            m_respawnConfirmed = false;

            if (m_debugMode) Debug.Log("Waiting for Respawn. Player : {_deadPlayer.OwnerClientId}");
            var _spawnPointForPlayer = GetRespawnPoints(_deadPlayer);
            if (_spawnPointForPlayer == null)
            {
                _deadPlayer.m_lifeCurrent.Value = -1;
                yield break;
            }

            m_timer.Value = 0f;

            while (m_timer.Value < m_respawnWaitTime)
            {
                if(m_respawnConfirmed)
                {
                    break;
                }
                m_timer.Value += Time.deltaTime;
                yield return null;
            }

            
            if (_spawnPointForPlayer != null) 
            {
                var _randomSpawn = _spawnPointForPlayer[Random.Range(0,_spawnPointForPlayer.Length)];
                _deadPlayer.SetSpawnPoint(_randomSpawn);
            }

            RespawnCharacter(_deadPlayer);

            m_waitingRespawnPlayer = null;
            m_isWaitingRespawn = false;
        }

        m_gamePhase.Value = GamePhase.ResolveCards;
    }
    

    private IEnumerator CardSelectionPhase()
    {
        m_gamePhase.Value = GamePhase.CardSelection;

        m_turnSelections.Clear();

        foreach(PlayerSession player in m_players)
        {
            PlayerTurnSelection selection = new PlayerTurnSelection
            {
                m_player = player,
                m_isConfirmed = false
            };

            m_turnSelections.Add(player, selection);
        }

        if (m_debugMode) Debug.Log("Card selection phase started.");

        m_timer.Value = 0f;

        while (m_timer.Value<m_cardSelectionTime)
        {
            if(AllPlayerConfirmedSelection())
            {
                break;
            }
            m_timer.Value += Time.deltaTime;
            yield return null;
        }

        BuildTurnOrderByCardCost();
    }
    

    private IEnumerator GameLoop()
    {
        while(!IsGameFinished())
        {
            yield return RoundBreakPhase();
            yield return DrawCardPhase();
            yield return CardSelectionPhase();
            yield return PlayerTurnPhase();
        }
    }

    private void CreateDeck()
    {
        m_deck.Clear();
        int _randomSeed = Random.Range(0, 2);
        int _maxAttackSpell = 0;
        int _maxDefenceSpell = 0;
        int _maxDrawCardSpell = 0;

        int _currentAttackSpell = 0;
        int _currentDefenceSpell = 0;
        int _currentDrawCardSpell = 0;
        switch(_randomSeed)
        {
            case 0:
                _maxAttackSpell = 20;
                _maxDefenceSpell = 30;
                _maxDrawCardSpell = 30;
                break;
            case 1:
                _maxAttackSpell = 30;
                _maxDefenceSpell = 20;
                _maxDrawCardSpell = 30;
                break;
            case 2:
                _maxAttackSpell = 30;
                _maxDefenceSpell = 30;
                _maxDrawCardSpell = 20;
                break;
        }
        List<CardData> _allAttackSpell = new List<CardData>();
        List<CardData> _allDefenceSpell = new List<CardData>();
        List<CardData> _allDrawCardSpell = new List<CardData>();

        List<CardData> _uniqueAttackSpell = new List<CardData>();
        List<CardData> _uniqueDefenceSpell = new List<CardData>();
        List<CardData> _uniqueDrawCardSpell = new List<CardData>();

        foreach(var card in m_cardDatabase.m_cards)
        {
            switch(card.m_specialSpecialization)
            {
                case CardSpecialization.AttackSpell:
                    _allAttackSpell.Add(card);
                    _uniqueAttackSpell.Add(card);
                    break;
                case CardSpecialization.DeffenceSpell:
                    _allDefenceSpell.Add(card);
                    _uniqueDefenceSpell.Add(card);
                    break;
                case CardSpecialization.CardDraftSpell:
                    _allDrawCardSpell.Add(card);
                    _uniqueDrawCardSpell.Add(card);
                    break;
            }
        }

        FillCardSection(_maxAttackSpell, _currentAttackSpell, _allAttackSpell, _uniqueAttackSpell);
        FillCardSection(_maxDefenceSpell,_currentDefenceSpell, _allDefenceSpell, _uniqueDefenceSpell);
        FillCardSection(_maxDrawCardSpell, _currentDrawCardSpell, _allDrawCardSpell, _uniqueDrawCardSpell);

        m_deck.Shuffle();  
    }
    private void FillCardSection(int maxCardType, int currentCardType, List<CardData> allList, List<CardData> uniqueList)
    {
        int _randomSeed;
        List<CardData> _nonUniqueList = new List<CardData>();
        while(maxCardType>currentCardType)
        {
            if (uniqueList.Count > 0)
            {
                _randomSeed = Random.Range(0, uniqueList.Count);
                int i = 0;
                int maxCard = 0;
                switch (uniqueList[_randomSeed].m_rarity)
                {
                    case CardRarity.Common:
                    case CardRarity.Uncommon:
                    case CardRarity.Rare:
                        maxCard = 4;
                        _nonUniqueList.Add(uniqueList[_randomSeed]);
                        break;
                    case CardRarity.UltraRare:
                        maxCard = 2;
                        _nonUniqueList[i] = uniqueList[_randomSeed];
                        break;
                    case CardRarity.Unique:
                        maxCard = 1;
                        break;
                }
                for (; i < maxCard; i++)
                {
                    NetworkCard _card = Instantiate(m_cardPrefab);
                    _card.m_cardData = uniqueList[_randomSeed];
                    _card.NetworkObject.Spawn();
                    _card.m_isFaceUp.Value = true;
                    m_spawnedCards.Add(_card);
                    m_deck.Add(_card);
                }
                currentCardType += maxCard;
                uniqueList.RemoveAt(_randomSeed);
            }
            else
            {
                if (_nonUniqueList.Count > 0)
                {
                    _randomSeed = Random.Range(0, _nonUniqueList.Count);
                    int i = 0;
                    int maxCard = 0;
                    switch (_nonUniqueList[_randomSeed].m_rarity)
                    {
                        case CardRarity.Common:
                        case CardRarity.Uncommon:
                        case CardRarity.Rare:
                            maxCard = 4;
                            break;
                        case CardRarity.UltraRare:
                            maxCard = 2;
                            break;
                    }
                    for (; i < maxCard; i++)
                    {
                        NetworkCard _card = Instantiate(m_cardPrefab);
                        _card.m_cardData = _nonUniqueList[_randomSeed];
                        _card.NetworkObject.Spawn();
                        _card.m_isFaceUp.Value = true;
                        m_spawnedCards.Add(_card);
                        m_deck.Add(_card);
                    }
                    currentCardType += maxCard;
                }
                else
                {
                    Debug.LogWarning("Not Enought card for section");
                    currentCardType = maxCardType;
                }
            }
        }
    }

    private void DrawCard(PlayerSession session)
    {
        var card = m_deck.ElementAt(0);
        session.AddCard(card);
        card.SetOwner(session.NetworkObjectId);
        SendCardDataClientRpc(card.NetworkObjectId, card.m_cardData.m_cardId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { session.OwnerClientId } } });
        m_deck.Remove(card);
        Debug.Log("Draw");
    }
    private IEnumerator DrawCardPhase()
    {
        Debug.Log("DrawPhase started");     
        m_timer.Value = 0;
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < m_players.Count; j++)
            {
                DrawCard(m_players[j]);
                yield return new WaitForSeconds(m_cardDrawDelay);
                m_timer.Value += m_cardDrawDelay;
            }
        }
        Debug.Log("Draw phase ended");
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
