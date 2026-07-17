using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

public enum SessionRole
{
    Player,
    Spectator
}
public interface ISelectable
{
    void SetSelected(bool selected);
}

public class PlayerSession : NetworkBehaviour
{
    private Camera m_camera;
    private GameObject m_currentSelect;
    private PlayerController m_currentSelectedCharacter;
    private BlockController m_currentSelectedBlock;

    public NetworkVariable<ulong> ClientId = new();

    public NetworkVariable<ulong> CharacterObjectId = new(0);

    public NetworkVariable<int> TeamId = new(0);

    private PlayerController m_currentCharacter;

    public bool m_debugMode = false;

    private SessionRole m_playerRole = SessionRole.Player;

    private int m_lifeMax = 0;
    public NetworkVariable<int> m_lifeCurrent = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private SpawnPoint m_spawnPoint;

    private BlockController[,] m_blocks = new BlockController[5,5];

    public NetworkList<ulong> m_handCards = new NetworkList<ulong>();
    public NetworkList<ulong> m_boardPlayCards = new NetworkList<ulong>();
    public NetworkList<ulong> m_boardDiscardCards = new NetworkList<ulong>();

    private NetworkCard m_selectedCard;

    private CardControllPackage m_currentCardControllPackage = CardControllPackage.CreateEmpty(0);

    [SerializeField] private CardPreviewController m_cardPreviewController;

    public NetworkVariable<ulong> m_currentOponentNetworkObjectId;

    private List<NetworkCard> m_selectedCardsToPlay = new();
    private ulong[] m_selectedCardsToPlayID;
    private List<NetworkCard> m_selectedCardsToDiscard = new();
    private ulong[] m_selectedCardsToDiscardID;

    private ulong m_respawnBlockNetworkId = 0;

    public void ConfirmTurn()
    {
        if (!IsOwner)
            return;

        m_selectedCardsToPlayID = ConvertCardsToNetworkId(m_selectedCardsToPlay);
        m_selectedCardsToDiscardID = ConvertCardsToNetworkId(m_selectedCardsToDiscard);

        ConfirmTurnServerRpc(
            m_selectedCardsToPlayID,
            m_selectedCardsToDiscardID,
            m_respawnBlockNetworkId
        );
       
        ClearLocalCardSelection();
    }

    [ServerRpc]
    private void ConfirmTurnServerRpc(
        ulong[] selectedCardsToPlayID,
        ulong[] selectedCardsToDiscardID,
        ulong respawnBlockNetworkId,
        ServerRpcParams rpcParams = default)
    {
        GameMaster.Instance.HandleConfirmTurn(
            rpcParams.Receive.SenderClientId,
            this,
            selectedCardsToPlayID,
            selectedCardsToDiscardID,
            respawnBlockNetworkId
        );
        Debug.Log("Rpc called");
    }

    private ulong[] ConvertCardsToNetworkId(List<NetworkCard> cards)
    {
        List<ulong> _result = new();
        foreach (NetworkCard card in cards)
        {
            if (card == null) continue;

            NetworkObject _netObject = card.GetComponent<NetworkObject>();
            if (_netObject == null) continue;

            _result.Add(_netObject.NetworkObjectId);
        }
        return _result.ToArray();
    }
    
    public bool SelectCardToPlay(NetworkCard card)
    {
        if(card == null) return false;
        if (!GameMaster.Instance.CanPlayerSelectCards(this)) return false;
        
        if(!m_selectedCardsToPlay.Contains(card)&& !m_selectedCardsToDiscard.Contains(card))
        { 
            m_selectedCardsToPlay.Add(card);
            return true;
        }
        else
        {
            m_selectedCardsToPlay.Remove(card);
            return false;
        }
    }
    public bool SelectCardToDiscard(NetworkCard card)
    {
        if (card == null) return false;
        if (!GameMaster.Instance.CanPlayerSelectCards(this)) return false;

        if (!m_selectedCardsToPlay.Contains(card) && !m_selectedCardsToDiscard.Contains(card))
        {
            m_selectedCardsToDiscard.Add(card);
            return true;
        }
        else
        {
            m_selectedCardsToDiscard.Remove(card);
            return false;
        }
    }
    
    public void ClearLocalCardSelection()
    {
        m_selectedCardsToPlay.Clear();
        m_selectedCardsToDiscard.Clear();
    }

    public void AddCard(NetworkCard card)
    {
        if(!IsServer)
        {
            return;
        }
        m_handCards.Add(card.NetworkObjectId);
    }

    public void RemoveCard(ulong cardNetworkObjectId)
    {
        if (!IsServer)
            return;

        if (m_handCards.Contains(cardNetworkObjectId))
            m_handCards.Remove(cardNetworkObjectId);
    }

    public IEnumerable<NetworkCard> GetHandCards()
    {
        foreach (var cardId in m_handCards)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject _obj))
            {
                NetworkCard card = _obj.GetComponent<NetworkCard>();
                if (card != null)
                {
                    yield return card;
                }
            }
        }
    }
    public IEnumerable<NetworkCard> GetBoardPlayCards()
    {
        foreach (var cardId in m_boardPlayCards)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject _obj))
            {
                NetworkCard card = _obj.GetComponent<NetworkCard>();
                if (card != null)
                {
                    yield return card;
                }
            }
        }
    }
    public IEnumerable<NetworkCard> GetBoardDiscardCards()
    {
        foreach (var cardId in m_boardDiscardCards)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject _obj))
            {
                NetworkCard card = _obj.GetComponent<NetworkCard>();
                if (card != null)
                {
                    yield return card;
                }
            }
        }
    }

    private void SelectObject()
    {
        //todo Preview
        Vector2 _mousePosition = Mouse.current.position.value;
        Ray _ray = m_camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        m_cardPreviewController.HidePreview();

        if (Physics.Raycast(_ray, out RaycastHit hit))
        {
            CardViewWorld _card = hit.collider.GetComponent<CardViewWorld>();

            if(_card != null)
            {
                m_cardPreviewController.ShowPreview(_card.GetNetworkCard(), _mousePosition);
            }
                m_currentSelect = hit.collider.gameObject;

            HandleSelection(m_currentSelect);
        }
    }
    private void HandleSelection(GameObject selectedObject)
    {
        if (selectedObject.TryGetComponent(out PlayerController playerController))
        {
            if(m_currentSelectedCharacter != null)
            {
                m_currentSelectedCharacter.SetSelected(false);
            }
            m_currentSelectedCharacter = playerController;
            playerController.SetSelected(true);
            if(m_currentSelectedBlock!=null)
            {
                m_currentSelectedBlock.SetSelected(false);
                m_currentSelectedBlock = null;
            }
            
        }
        else if (selectedObject.TryGetComponent(out BlockController blockController))
        {
            if(m_currentSelectedBlock != null)
            {
               m_currentSelectedBlock.SetSelected(false);
            }
            m_currentSelectedBlock = blockController;
            blockController.SetSelected(true);
            if(m_currentSelectedCharacter!=null)
            {
                m_currentSelectedCharacter.SetSelected(false);
                m_currentSelectedCharacter = null;
            }
            
        }
        else
        {
            if(m_currentSelectedCharacter != null)
            {
                m_currentSelectedCharacter.SetSelected(false);
                m_currentSelectedCharacter = null;
            }
            if(m_currentSelectedBlock != null)
            {
                m_currentSelectedBlock.SetSelected(false);
                m_currentSelectedBlock = null;
            }
        }
    }

    public void AddBlock(BlockController block,int x, int y)
    {
        if (m_blocks != null)
        {
            m_blocks[x, y] = block;
            block.GetComponentInChildren<SpawnPoint>().Initialize(TeamId.Value);
            block.SetSession(this);
        }
        else
        {
            return;
        }
    }
    
    public void DeleteBlock(BlockController block)
    {
        if (m_blocks != null)
        {
            for (int x = 0; x < m_blocks.GetLength(0); x++)
            {
                for (int y = 0; y < m_blocks.GetLength(1); y++)
                {
                    if (m_blocks[x, y] == block)
                    {
                        m_blocks[x, y] = null;
                        return;
                    }
                }
            }
        }
    }

    public void ClearBlocks()
    {
        foreach (var block in m_blocks)
        {
            if (block == null)
                continue;

            if (block.NetworkObject != null &&
                block.NetworkObject.IsSpawned)
            {
                block.NetworkObject.Despawn(true);
            }
        }
    }
    public void SetSpawnPoint(SpawnPoint point)
    {
        m_spawnPoint = point;
        transform.position = point.transform.position;
    }

    public override void OnNetworkSpawn()
    {
        if(IsOwner)
        {
            SessionManager.Instance.LocalPlayer = this;
            m_camera = Camera.main;
        }
    }
    public SessionRole GetSessionRole()
    {
        return m_playerRole;
    }
    public void Initialize(ulong clientId, int teamId)
    {
        ClientId.Value = clientId;
        if (m_debugMode)
        {
            Debug.Log("Player session initialized for client: " + clientId);
        }
        TeamId.Value = teamId;
        if (m_debugMode)
        {
            Debug.Log("Assigned team " + TeamId.Value + " to client: " + clientId);
        }

        m_lifeMax = 3;
        m_lifeCurrent.Value = m_lifeMax;
    }


    public void SetCharacter(PlayerController playerController)
    {
        m_currentCharacter = playerController;
    }

    public PlayerController GetCharacter()
    {
        if(m_currentCharacter == null)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(CharacterObjectId.Value, out NetworkObject _obj))
            {
                m_currentCharacter = _obj.GetComponent<PlayerController>();
                if (IsServer)
                {
                    CharacterObjectId.Value = m_currentCharacter.GetComponent<NetworkObject>().NetworkObjectId;
                }
            }
            else
            {
                if(m_debugMode)Debug.Log("No Character");
                return null;
            }
        }
        if(CharacterObjectId.Value == 0)
        {
            if(m_debugMode)
            {
                Debug.Log("CharacterObjId is 0");
            }
            return null;
        }
        return m_currentCharacter;
    }

    public void MovePlayeToSpawnPoint(float deltaY = 3.0f)
    {
        if(!IsServer)
        {
            return;
        }

        m_currentCharacter.transform.position = m_spawnPoint.transform.position + Vector3.up*deltaY;
    }
    public SpawnPoint GetSpawnPoint()
    {
        return m_spawnPoint;
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }
        if(Mouse.current.leftButton.wasPressedThisFrame)
        {
            SelectObject();
        }
    }

    private void ClearHand()
    {
        foreach (var cardId in m_handCards)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject _obj))
            {
                NetworkCard card = _obj.GetComponent<NetworkCard>();
                if (card != null)
                {
                    card.NetworkObject.Despawn(true);
                }
            }
        }
        m_handCards.Clear();
    }

    public void ClearSession()
    {
               ClearHand();
        ClearBlocks();
        if (m_currentCharacter != null && m_currentCharacter.NetworkObject != null && m_currentCharacter.NetworkObject.IsSpawned)
        {
            m_currentCharacter.NetworkObject.Despawn(true);
        }
        m_currentCharacter = null;
    }

    public void SetSelectedCard(NetworkCard card)
    {
        m_selectedCard = card;
    }

    public NetworkCard GetSelectedCard()
    {
        return m_selectedCard;
    }
    public void ClearSelectedCard()
    {
        m_selectedCard = null;
    }

    private void ConfirmSelectTarget(
        int index,
        ulong targetNetworkObjectId)
    {
        if (index < 0)
        {
            Debug.LogWarning(
                $"Invalid target index: {index}");

            return;
        }

        if (targetNetworkObjectId == 0)
        {
            Debug.LogWarning(
                $"Invalid target NetworkObjectId for index {index}");

            return;
        }

        m_currentCardControllPackage.SetTarget(
            index,
            targetNetworkObjectId);

        if (m_debugMode)
        {
            Debug.Log(
                $"Target confirmed. " +
                $"Index: {index}, " +
                $"NetworkObjectId: {targetNetworkObjectId}");
        }
    }
    //todo need rework?
    public bool TryConfirmTarget()
    {
        if(m_currentCardControllPackage.CardNetworkObjectId == 0)
        {
            if (m_selectedCard != null)
            {
                m_currentCardControllPackage.CardNetworkObjectId = m_selectedCard.NetworkObjectId;
            }
            else 
            {
                Debug.LogWarning("No card selected for control package.");
                return false;
            }
        }
        if(m_currentCardControllPackage.CardNetworkObjectId != m_selectedCard.NetworkObjectId)
        {
            m_currentCardControllPackage = CardControllPackage.CreateEmpty(m_selectedCard.NetworkObjectId);
        }
        if (GameMaster.Instance.m_gamePhase.Value != GamePhase.RespawnWait)
        {
            if (m_selectedCard == null || m_selectedCard.NetworkObjectId != m_currentCardControllPackage.CardNetworkObjectId)
            {
                Debug.LogWarning("Selected card does not match control package card.");
                return false;
            }
        }
        if(m_selectedCard.CheckActiveTaergetSelection() == false)
        {
            AutoSelectTarget();
            if (m_debugMode) Debug.Log("Card dont have selective target");
            return true;
        }
        PlayerSession _oponent = GameMaster.Instance.GetSessionByID(m_currentOponentNetworkObjectId.Value);
        if(_oponent == null)
        {
            Debug.LogWarning("NoOponent "+m_currentOponentNetworkObjectId.Value); return false;
        }
        if (GameMaster.Instance.m_gamePhase.Value != GamePhase.RespawnWait)
        {
            for (int i = 0; i < m_selectedCard.m_cardData.m_activities.Length; i++)
            {
                if (m_currentCardControllPackage.GetTarget(i) != 0)
                {
                    continue;
                }
                else
                {
                    switch (m_selectedCard.m_cardData.m_activities[i].s_targetType)
                    {
                        case CardTargetType.SelfPlayer:
                            if (GetCharacter() != null)
                                ConfirmSelectTarget(i, this.NetworkObjectId);
                            break;
                        case CardTargetType.AnyPlayer:
                            if (m_currentSelectedCharacter != null)
                            {
                                ConfirmSelectTarget(i, m_currentSelectedCharacter.GetSession().NetworkObjectId);
                            }
                            else if (m_currentSelectedBlock != null)
                            {
                                ConfirmSelectTarget(i, m_currentSelectedBlock.GetSession().NetworkObjectId);
                            }
                            else
                            {
                                Debug.LogWarning("No character selected for target.");
                                return false;
                            }
                            break;
                        case CardTargetType.OpponentPlayer:
                            if (_oponent != null && _oponent.GetCharacter() != null)
                            {
                                ConfirmSelectTarget(i, _oponent.NetworkObjectId);
                            }
                            break;
                        case CardTargetType.AnyPlayerBlock:
                            if (m_currentSelectedBlock != null)
                            {
                                ConfirmSelectTarget(i, m_currentSelectedBlock.NetworkObjectId);
                            }
                            else
                            {
                                Debug.LogWarning("No block selected for target.");
                                return false;
                            }
                            break;
                        case CardTargetType.OpponentBlock:
                            if (m_currentSelectedBlock != null && m_currentSelectedBlock.m_teamId.Value == _oponent.TeamId.Value)
                            {
                                ConfirmSelectTarget(i, m_currentSelectedBlock.NetworkObjectId);
                            }
                            else
                            {
                                Debug.LogWarning("No opponent block selected for target.");
                                return false;
                            }
                            break;
                        case CardTargetType.SelfBlock:
                            if ((m_currentSelectedBlock != null) && (m_currentSelectedBlock.m_teamId.Value == TeamId.Value))
                            {
                                ConfirmSelectTarget(i, m_currentSelectedBlock.NetworkObjectId);
                            }
                            else
                            {
                                Debug.LogWarning("No self block selected for target.");
                                return false;
                            }
                            break;
                        case CardTargetType.RandomOpponentBlock:
                        case CardTargetType.AllBlockOpponent:
                        case CardTargetType.AllBlockSelf:
                        case CardTargetType.RandomBlockSelectedPlayer:
                        case CardTargetType.RandomSelfBlock:
                            break;
                    }
                }
            }
        }
        else
        {
            if(m_currentSelectedBlock != null)
            {
                m_respawnBlockNetworkId = m_currentSelectedBlock.NetworkObjectId;
            }
        }
        return true;    
    }
    [ServerRpc]
    public void CardInputServerRpc(CardControllPackage package, ServerRpcParams rpcParams = default)
    {
        try
        {
            if (m_debugMode) Debug.Log("Rpc Called");

            if (GameMaster.Instance == null)
            {
                Debug.LogError("GameMaster instance null");
                return;
            }
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            bool _handleResult = GameMaster.Instance.HandleCardControlPackage(package,senderClientId);
            if (m_debugMode)
            {
                if (_handleResult)
                {
                    Debug.Log("Handle finished");
                    return;
                }
                else
                {
                    Debug.Log("Handle unseccess");
                    return;
                }
            }
            return;
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }
    }

    public void TryHandlePackage()
    {
        var package = m_currentCardControllPackage;
        if(CheckPackage(package))
        {
            if (GameMaster.Instance.CanPlayerSelectCardTargets(this))
            {
                CardInputServerRpc(package);
                m_currentCardControllPackage = CardControllPackage.CreateEmpty(0);
            }
        }
    }
    private bool CheckPackage(CardControllPackage package)
    {
        if (package.CardNetworkObjectId != 0)
        {
            var _targetData = m_selectedCard.m_cardData.m_activities;
            for (int i = 0; i < _targetData.Length; i++)
            {
                if (
                    !(_targetData[i].s_targetType == CardTargetType.AnyPlayer||
                    _targetData[i].s_targetType == CardTargetType.AnyPlayerBlock||
                    _targetData[i].s_targetType == CardTargetType.OpponentBlock||
                    _targetData[i].s_targetType == CardTargetType.OpponentPlayer||
                    _targetData[i].s_targetType == CardTargetType.RandomBlockSelectedPlayer||
                    _targetData[i].s_targetType == CardTargetType.None)
                    && package.TargetNetworkObjectId[i] == 0)
                {
                    return false;
                }
            }
        }
        return true;
    }
    public void SetOponent(ulong oponentId)
    {
        m_currentOponentNetworkObjectId.Value = oponentId;
    }
    //todo
    private void AutoSelectTarget()
    {
        for(int i = 0;i< m_selectedCard.m_cardData.m_activities.Length;i++)
        {
            switch(m_selectedCard.m_cardData.m_activities[i].s_targetType)
            {
                case CardTargetType.OpponentPlayer:
                    m_currentCardControllPackage.SetTarget(i,m_currentOponentNetworkObjectId.Value);
                    break;
                case CardTargetType.SelfPlayer:
                    m_currentCardControllPackage.SetTarget(i, this.NetworkObjectId);
                    break;
                default:
                    m_currentCardControllPackage.SetTarget(i, 0);
                    break;
            }
            
        }
    }
    public BlockController[,] GetSessionBlock()
    {
        return m_blocks;
    }
}
