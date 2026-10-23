using System.Collections.Generic;
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

        if (PlayerUI.Instance != null)
            PlayerUI.Instance.OnCardDataReceived(cardNetworkObjectId);
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
            if (!IsServer)
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

            if (_package.TargetNetworkObjectId == null || _package.TargetNetworkObjectId.Count == 0 ||
                _package.TargetNetworkObjectId.Count > CardControllPackage.MaxTargets)
            {
                int _count = _package.TargetNetworkObjectId != null ? _package.TargetNetworkObjectId.Count : 0;
                Debug.LogWarning($"Card package rejected. Invalid target count: {_count}.");
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

                if (SessionManager.Instance != null)
                {
                    PlayerSession _senderSession = SessionManager.Instance.GetPlayerSession(senderClientId);
                    if (_senderSession != null)
                    {
                        _senderSession.ReportSuspicious(SuspiciousActionType.ForeignCardPackage,
                            $"Card {_card.NetworkObjectId} belongs to session {_card.m_ownerId.Value}.");
                    }
                }

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
    /// <param name="intendedPhase">The phase the player sent the confirmation for.</param>
    /// <param name="playCardNetworkIds">The network IDs of the cards to play.</param>
    /// <param name="discardCardNetworkIds">The network IDs of the cards to discard.</param>
    /// <param name="respawnBlockNetworkId">The network ID of the respawn block.</param>
    public void HandleConfirmTurn(ulong senderClientId, PlayerSession playerSession,
                                  GamePhase intendedPhase, ulong[] playCardNetworkIds, ulong[] discardCardNetworkIds, ulong respawnBlockNetworkId)
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
            if (SessionManager.Instance != null)
            {
                PlayerSession _senderSession = SessionManager.Instance.GetPlayerSession(senderClientId);
                if (_senderSession != null)
                {
                    _senderSession.ReportSuspicious(SuspiciousActionType.RpcSenderMismatch,
                        $"Confirm sender {senderClientId} does not own session {playerSession.NetworkObjectId}.");
                }
            }
            return;
        }

        if (!IsConfirmForCurrentPhase(intendedPhase, m_gamePhase.Value))
        {
            if (m_debugMode)
                Debug.Log($"Confirm ignored: sent for {intendedPhase}, server is in {m_gamePhase.Value}.");
            return;
        }

        switch (m_gamePhase.Value)
        {
            case GamePhase.CardSelection:
                {
                    if (!HandleConfirmSelectionTurn(playerSession, playCardNetworkIds, discardCardNetworkIds,
                            out PlayerTurnSelection turnSelections))
                    {
                        return;
                    }

                    playerSession.m_boardPlayCards.Clear();
                    playerSession.m_boardDiscardCards.Clear();

                    foreach (NetworkCard card in turnSelections.m_cardsToPlay)
                    {
                        card.SetLocation(CardLocation.Board);
                        card.m_isRotated.Value = false;

                        SendCardDataClientRpc(card.NetworkObjectId, card.m_cardData.m_cardId);

                        playerSession.m_boardPlayCards.Add(card.NetworkObjectId);
                    }

                    foreach (NetworkCard card in turnSelections.m_cardToDiscard)
                    {
                        card.SetLocation(CardLocation.Board);
                        card.m_isRotated.Value = true;

                        SendCardDataClientRpc(card.NetworkObjectId, card.m_cardData.m_cardId);

                        playerSession.m_boardDiscardCards.Add(card.NetworkObjectId);
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
                if (m_debugMode) Debug.Log($"Confirm ignored. Current phase: {m_gamePhase.Value}");
                break;
        }

    }
    private static bool IsConfirmForCurrentPhase(GamePhase intendedPhase, GamePhase currentPhase)
    {
        switch (intendedPhase)
        {
            case GamePhase.CardSelection:
                return currentPhase == GamePhase.CardSelection;
            case GamePhase.PlayerAction:
            case GamePhase.ResolveCards:
                return currentPhase == GamePhase.PlayerAction || currentPhase == GamePhase.ResolveCards;
            case GamePhase.RespawnWait:
                return currentPhase == GamePhase.RespawnWait;
            default:
                return false;
        }
    }
    /// <summary>
    /// Handles the confirmation of a player's card selection during their turn. This method is called on the server and processes the play and discard card network IDs, updating the player's turn selections accordingly.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="playCardNetworkId"></param>
    /// <param name="discardCardNetworkId"></param>
    /// <param name="turnSelections">The accepted card selection for this session.</param>
    /// <returns>True if this is the session's first accepted confirmation in this selection phase.</returns>
    private bool HandleConfirmSelectionTurn(PlayerSession session, ulong[] playCardNetworkId, ulong[] discardCardNetworkId,
                                           out PlayerTurnSelection turnSelections)
    {

        if (!m_turnSelections.TryGetValue(session, out turnSelections))
        {
            Debug.LogWarning("No selection found for session");
            return false;
        }

        if (turnSelections.m_isConfirmed)
        {
            if (m_debugMode) Debug.Log("Selection already confirmed");
            return false;
        }

        turnSelections.m_cardsToPlay.Clear();
        turnSelections.m_cardToDiscard.Clear();

        HashSet<ulong> usedIds = new();
        List<ulong> rejectedIds = new();

        ValidateSelectionCards(session, playCardNetworkId, turnSelections.m_cardsToPlay, usedIds, rejectedIds);
        ValidateSelectionCards(session, discardCardNetworkId, turnSelections.m_cardToDiscard, usedIds, rejectedIds);

        if (rejectedIds.Count > 0)
        {
            Debug.LogWarning($"Card selection rejected IDs for player {session.OwnerClientId}: {string.Join(", ", rejectedIds)}.");
            session.ReportSuspicious(SuspiciousActionType.InvalidCardSelection,
                $"Rejected card IDs: {string.Join(", ", rejectedIds)}.");
        }

        int _playCost = 0;
        foreach (NetworkCard card in turnSelections.m_cardsToPlay)
        {
            _playCost += card.m_cardData.m_cost;
        }

        if (_playCost > session.m_manaCurrent.Value)
        {
            turnSelections.m_cardsToPlay.Clear();
            turnSelections.m_cardToDiscard.Clear();
            session.ReportSuspicious(SuspiciousActionType.ManaOverBudget,
                $"cost {_playCost} > mana {session.m_manaCurrent.Value}");
            session.SelectionRejectedClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { session.OwnerClientId }
                }
            });
            ResetCardSelectionTimer(session);
            return false;
        }

        AddMana(session, -_playCost);
        turnSelections.m_isConfirmed = true;

        if (m_debugMode)
        {
            Debug.Log($"Player {session.OwnerClientId} confirmed card selection. Cost: {turnSelections.TotalCost()}");
        }

        return true;
    }

    private void ValidateSelectionCards(PlayerSession session, ulong[] cardNetworkIds, List<NetworkCard> acceptedCards,
                                        HashSet<ulong> usedIds, List<ulong> rejectedIds)
    {
        if (cardNetworkIds == null)
            return;

        foreach (ulong cardId in cardNetworkIds)
        {
            if (cardId == 0 || usedIds.Contains(cardId) || !ContainsNetworkObjectId(session.m_handCards, cardId))
            {
                rejectedIds.Add(cardId);
                continue;
            }

            NetworkCard _card = GetCardByID(cardId);
            if (_card == null || _card.m_ownerId.Value != session.NetworkObjectId || _card.m_cardData == null)
            {
                rejectedIds.Add(cardId);
                continue;
            }

            usedIds.Add(cardId);
            acceptedCards.Add(_card);
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
