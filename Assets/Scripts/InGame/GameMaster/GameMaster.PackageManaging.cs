using Unity.Netcode;
using UnityEngine;

public partial class GameMaster
{
    /// <summary>
    /// Provides card data to the client for a specific card network object ID and card ID. This method is called on the server and executed on the client side.
    /// </summary>
    /// <param name="cardNetworkObjectId">The network object ID of the card.</param>
    /// <param name="cardId">The ID of the card.</param>
    /// <param name="clientRpcParams">The client RPC parameters.</param>
    [ClientRpc]
    public void SendCardDataClientRpc(ulong cardNetworkObjectId, string cardId, ClientRpcParams clientRpcParams = default)
    {
        if (m_debugMode)
        {
            Debug.Log(
                $"SendCardDataClientRpc called. " +
                $"Card: {cardNetworkObjectId}, CardId: {cardId}");
        }

        NetworkCard _card = GetCardByID(cardNetworkObjectId);

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
    /// <summary>
    /// Handles a player control package received from a client. This method is called on the server and processes the input package, adding it to the input queue for further processing.
    /// </summary>
    /// <param name="_package">The player control package to handle.</param>
    /// <returns>True if the package was successfully handled; otherwise, false.</returns>
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
    
    /// <summary>
    /// Handles a card control package received from a client. This method is called on the server and processes the card input package, adding it to the card input queue for further processing.
    /// </summary>
    /// <param name="_package">The card control package to handle.</param>
    /// <param name="senderClientId">The client ID of the sender.</param>
    /// <returns>True if the package was successfully handled; otherwise, false.</returns>
    public bool HandleCardControlPackage(CardControllPackage _package, ulong senderClientId)
    {
        try
        {
            if (!IsServer)
            {
                if (m_debugMode)
                {
                    Debug.Log("Its Not server GM");
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

            NetworkCard _card = GetCardByID(_package.CardNetworkObjectId);

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

            if (!ContainsNetworkObjectId(_activePlayer.m_boardPlayCards, _card.NetworkObjectId))
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
    
    /// <summary>
    /// Handles the confirmation of a player's turn. This method is called on the server and processes the play and discard card network IDs, updating the player's board state accordingly.
    /// </summary>
    /// <param name="senderClientId">The client ID of the sender.</param>
    /// <param name="playerSession">The player session of the sender.</param>
    /// <param name="playCardNetworkIds">The network IDs of the cards to play.</param>
    /// <param name="discardCardNetworkIds">The network IDs of the cards to discard.</param>
    /// <param name="respawnBlockNetworkId">The network ID of the respawn block.</param>
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
                    HandleConfirmSelectionTurn(playerSession, playCardNetworkIds, discardCardNetworkIds);

                    playerSession.m_boardPlayCards.Clear();
                    playerSession.m_boardDiscardCards.Clear();

                    foreach (ulong cardNetworkObjectId in playCardNetworkIds)
                    {
                        NetworkCard card = GetCardByID(cardNetworkObjectId);

                        if (card == null)
                            continue;

                        if (card.m_cardData == null)
                        {
                            Debug.LogWarning($"CardData is null for play card " + $"{cardNetworkObjectId}.");

                            continue;
                        }

                        card.SetLocation(CardLocation.Board);
                        card.m_isRotated.Value = false;

                        SendCardDataClientRpc(card.NetworkObjectId, card.m_cardData.m_cardId);

                        playerSession.m_boardPlayCards.Add(cardNetworkObjectId);
                    }

                    foreach (ulong cardNetworkObjectId in discardCardNetworkIds)
                    {
                        NetworkCard card = GetCardByID(cardNetworkObjectId);

                        if (card == null)
                            continue;

                        if (card.m_cardData == null)
                        {
                            Debug.LogWarning($"CardData is null for discard card " + $"{cardNetworkObjectId}.");

                            continue;
                        }

                        card.SetLocation(CardLocation.Board);
                        card.m_isRotated.Value = true;

                        SendCardDataClientRpc(card.NetworkObjectId, card.m_cardData.m_cardId);

                        playerSession.m_boardDiscardCards.Add(cardNetworkObjectId);
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
    /// <summary>
    /// Short method to send a package to the client using a ClientRpc. This method is called on the server and executed on the client side. Only for debugging purposes. The actual input handling should be done through the HandlePlayerControlPackage method.
    /// </summary>
    /// <param name="package"></param>
    /// <param name="clientRpcParams"></param>
    [ClientRpc]
    private void SendToClientRpc(ToPlayerControlPackage package, ClientRpcParams clientRpcParams = default)
    {
        if (m_debugMode) Debug.Log("SendToClientRpc");
    }
    /// <summary>
    /// Handles the confirmation of a player's card selection during their turn. This method is called on the server and processes the play and discard card network IDs, updating the player's turn selections accordingly.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="playCardNetworkId"></param>
    /// <param name="discardCardNetworkId"></param>
    private void HandleConfirmSelectionTurn(PlayerSession session, ulong[] playCardNetworkId, ulong[] discardCardNetworkId)
    {

        if (!m_turnSelections.TryGetValue(session, out var turnSelections))
        {
            Debug.LogWarning("No selection found for session");
            return;
        }

        if (turnSelections.m_isConfirmed)
        {
            if (m_debugMode) Debug.Log("Selection already confirmed");
            return;
        }

        turnSelections.m_cardsToPlay.Clear();
        turnSelections.m_cardToDiscard.Clear();

        foreach (ulong cardId in playCardNetworkId)
        {
            NetworkCard _card = GetCardByID(cardId);
            if (_card == null) continue;

            if (ValidateCardOwner(_card, session))
            {
                turnSelections.m_cardsToPlay.Add(_card);
            }
        }
        foreach (ulong cardId in discardCardNetworkId)
        {
            NetworkCard _card = GetCardByID(cardId);
            if (_card == null) continue;

            if (ValidateCardOwner(_card, session))
            {
                turnSelections.m_cardToDiscard.Add(_card);
            }
        }

        turnSelections.m_isConfirmed = true;

        if (m_debugMode)
        {
            Debug.Log($"Player {session.OwnerClientId} confirmed card selection. Cost: {turnSelections.TotalCost()}");
        }
    }
    /// <summary>
    /// Handles the confirmation of a player's action during the action phase. This method is called on the server and updates the active player's confirmed action status.
    /// </summary>
    /// <param name="player"></param>
    public void HandlePlayerActionConfirm(PlayerSession player)
    {
        if (player != m_activePlayer.Value)
        {
            Debug.LogWarning("Only active player can confirm action phase.");
            return;
        }
        m_activePlayerConfirmedAction = true;
    }
    /// <summary>
    /// Handles the confirmation of a player's respawn point. This method is called on the server and validates the respawn point before updating the player's respawn status.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="networkObjectId"></param>
    private void HandlePlayerRespawnPoint(PlayerSession player, ulong networkObjectId)
    {
        if (!IsServer)
            return;

        if (player == null)
            return;

        if (networkObjectId == 0)
        {
            Debug.LogWarning("Respawn point rejected: NetworkObjectId is 0.");

            return;
        }

        if (!m_waitingRespawnPlayer.Value.TryGet(out PlayerSession _waitingPlayer))
        {
            Debug.LogWarning("Respawn point rejected: no waiting player.");

            return;
        }

        if (player != _waitingPlayer)
        {
            Debug.LogWarning("Respawn point rejected: wrong player.");

            return;
        }

        BlockController _block = GetBlockById(networkObjectId);

        if (!IsValidRespawnBlock(player, _block))
        {
            Debug.LogWarning("Respawn point rejected: invalid block.");

            return;
        }

        m_respawnBlockNetworkId = networkObjectId;

        m_respawnConfirmed = true;

        if (m_debugMode)
        {
            Debug.Log("Respawn point confirmed. Player: " + player.OwnerClientId + " Block: " + networkObjectId);
        }
    }
    /// <summary>
    /// Validates if the card belongs to the player session. This method checks if the team ID of the player session matches the team ID of the owner of the card.
    /// </summary>
    /// <param name="card"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    private bool ValidateCardOwner(NetworkCard card, PlayerSession session)
    {
        return (session.TeamId.Value == GetSessionByID(card.m_ownerId.Value).TeamId.Value);
    }
    /// <summary>
    /// Validates if the respawn block is valid for the player. This method checks if the block is spawned, belongs to the same team as the player, and has a valid spawn point.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="block"></param>
    /// <returns></returns>
    private bool IsValidRespawnBlock(PlayerSession player, BlockController block)
    {
        if (player == null)
            return false;

        if (block == null)
            return false;

        if (block.NetworkObject == null ||
            !block.NetworkObject.IsSpawned)
        {
            return false;
        }

        if (block.m_teamId.Value != player.TeamId.Value)
        {
            return false;
        }

        if (block.GetSpawn() == null)
            return false;

        return true;
    }
}
