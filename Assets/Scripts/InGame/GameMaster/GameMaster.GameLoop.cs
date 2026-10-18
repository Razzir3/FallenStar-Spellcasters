using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class GameMaster
{
    #region BlockTurn
    /// <summary>
    /// Checks if the player can move during their turn.
    /// </summary>
    /// <param name="session">The player session to check.</param>
    /// <returns>True if the player can move, otherwise false.</returns>
    public bool CanPlayerMove(PlayerSession session)
    {
        return m_gamePhase.Value == GamePhase.PlayerAction && m_activePlayer.Value.TryGet(out PlayerSession player) && player == session
            && !m_isResolvingCards && !m_isWaitingRespawn;
    }
    /// <summary>
    /// Checks if the player can select card targets during their turn.
    /// </summary>
    /// <param name="session">The player session to check.</param>
    /// <returns>True if the player can select card targets, otherwise false.</returns>
    public bool CanPlayerSelectCardTargets(PlayerSession session)
    {
        return m_gamePhase.Value == GamePhase.PlayerAction && m_activePlayer.Value.TryGet(out PlayerSession player) && player == session
            && !m_isResolvingCards && !m_isWaitingRespawn;
    }
    /// <summary>
    /// Checks if the player can select cards during the card selection phase.
    /// </summary>
    /// <param name="session">The player session to check.</param>
    /// <returns>True if the player can select cards, otherwise false.</returns>
    public bool CanPlayerSelectCards(PlayerSession session)
    {
        return m_gamePhase.Value == GamePhase.CardSelection
            && !m_isResolvingCards && !m_isWaitingRespawn;
    }
    #endregion
    /// <summary>
    /// Main game loop coroutine that manages the flow of the game through its various phases until the game is finished.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    private IEnumerator GameLoop()
    {
        while (!IsGameFinished())
        {
            yield return RoundBreakPhase();
            yield return DrawCardPhase();
            yield return CardSelectionPhase();
            yield return PlayerTurnPhase();
        }
        switch (m_isLocalPlayerLost)
        {
            case true:
                Debug.LogError("Game finished. You lost.");
                break;
            case false:
                Debug.LogError("Game finished. You won.");
                break;
        }
    }
    /// <summary>
    /// Processes each player's turn in the order determined by the turn order list. It handles the player action phase for each player and checks if the game is finished after each turn.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
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
    /// <summary>
    ///  Processes the action phase for a specific player, allowing them to perform actions with their cards. It handles card input, validates actions, and resolves card effects. The phase continues until the player confirms their action or the action time limit is reached.
    /// </summary>
    /// <param name="player">The player session whose action phase is being processed.</param>
    /// <returns>An IEnumerator for coroutine handling.</returns>
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
            if (m_externalDeathDetected || IsAnyPlayerDead())
            {
                m_externalDeathDetected = false;

                if (IsGameFinished())
                {
                    break;
                }

                yield return WaitForRespawnIfNeeded();

                if (!IsGameFinished())
                {
                    m_gamePhase.Value =
                        GamePhase.PlayerAction;

                    m_activePlayer.Value =
                        player;
                }
            }
            while (m_cardInputQueue.Count > 0)
            {
                CardControllPackage _currentMessage = m_cardInputQueue.Dequeue();

                NetworkCard _currentCard = GetCardByID(_currentMessage.CardNetworkObjectId);

                if (_currentCard == null)
                {
                    Debug.LogWarning(
                        $"NetworkCard " +
                        $"{_currentMessage.CardNetworkObjectId} " +
                        $"was not found.");

                    continue;
                }

                if (_currentCard.m_ownerId.Value !=player.NetworkObjectId)
                {
                    Debug.LogWarning(
                        $"Player {player.NetworkObjectId} tried to use " +
                        $"card {_currentCard.NetworkObjectId} owned by " +
                        $"{_currentCard.m_ownerId.Value}.");

                    continue;
                }

                if (!ContainsNetworkObjectId(player.m_boardPlayCards,_currentCard.NetworkObjectId))
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

                int _activityCount = _currentCard.m_cardData.m_activities.Length;

                int _targetCount = _currentMessage.TargetNetworkObjectId.Count;

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
                        CardActivity _activity = _currentCard.m_cardData.m_activities[i];

                        ulong _targetNetworkObjectId = _currentMessage.GetTarget(i);

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

                        if (!CheckActivity(_activity,_targetNetworkObjectId,_currentCard.m_ownerId.Value))
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
                    CardActivity _activity = _currentCard.m_cardData.m_activities[i];

                    ulong _targetNetworkObjectId = _currentMessage.GetTarget(i);

                    ProcessActivity(_activity, _targetNetworkObjectId, _currentCard.m_ownerId.Value);

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

            if (m_activePlayerConfirmedAction &&m_cardInputQueue.Count == 0 &&!m_isResolvingCards)
            {
                break;
            }

            m_timer.Value += Time.deltaTime;

            yield return null;
        }

        m_isResolvingCards = false;

        ClearDiscardedCards(player);
        ClearPlayedCards(player);

        if (m_debugMode)
        {
            Debug.Log(
                $"Player Action phase ended. " +
                $"Active player: {player.OwnerClientId}");
        }
    }
    /// <summary>
    ///  Performs the round break phase, allowing for a pause between rounds. It sets the game phase to RoundBreak and waits for the specified round break time before proceeding to the next phase.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    private IEnumerator RoundBreakPhase()
    {
        m_gamePhase.Value = GamePhase.RoundBreak;

        if (m_debugMode)
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

        if (!m_turnSelections.TryGetValue(player, out PlayerTurnSelection selection))
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
                        if (IsAnyPlayerDead())
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
    /// <summary>
    /// Performs the card selection phase, allowing players to select cards for their turn. It sets the game phase to CardSelection, initializes player selections, and waits for all players to confirm their selections or until the card selection time limit is reached. After the selection phase, it builds the turn order based on card costs.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    private IEnumerator CardSelectionPhase()
    {
        m_gamePhase.Value = GamePhase.CardSelection;

        m_turnSelections.Clear();

        foreach (PlayerSession player in m_players)
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

        while (m_timer.Value < m_cardSelectionTime)
        {
            if (AllPlayerConfirmedSelection())
            {
                break;
            }
            m_timer.Value += Time.deltaTime;
            yield return null;
        }

        BuildTurnOrderByCardCost();
    }
    /// <summary>
    /// Performs the draw card phase, allowing each player to draw a specified number of cards. It sets the game phase to DrawCard, initializes the timer, and iterates through each player to draw cards with a delay between each draw. After all players have drawn their cards, the phase ends.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    private IEnumerator DrawCardPhase()
    {
        Debug.Log("DrawPhase started");
        m_timer.Value = 0;
        for (int i = 0; i < m_startHandCards; i++)
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
    /// <summary>
    /// Searches for any player who has lost all their lives and updates the game state accordingly. It checks each player's current life value, and if a player is found to have zero or less life, it records their network object ID, sets the external death detected flag, and determines if the lost player is the local player.
    /// </summary>
    private void FindLostPlayer()
    {
        foreach (PlayerSession player in m_players)
        {
            if (player == null)
            {
                Debug.LogError("GameMaster: player is null");
                continue;
            }
            if (player.m_lifeCurrent.Value > 0)
                continue;
            m_lostClientId = player.NetworkObjectId;
            bool _isLocalPlayer = SessionManager.Instance.LocalPlayer != null && player.NetworkObjectId == SessionManager.Instance.LocalPlayer.NetworkObjectId;
            m_externalDeathDetected = true;
            m_isLocalPlayerLost = _isLocalPlayer;
        }
    }
    /// <summary>
    /// Performs the respawn wait phase, allowing dead players to respawn. It checks for any dead players, sets the game phase to RespawnWait, and waits for the respawn confirmation or until the respawn wait time is reached. If a valid respawn point is found, the player is respawned; otherwise, their life is set to -1, indicating they cannot respawn.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    private IEnumerator WaitForRespawnIfNeeded()
    {
        while (IsAnyPlayerDead())
        {
            PlayerSession _deadPlayer = FindDeadPlayer();

            if (_deadPlayer == null)
            {
                yield break;
            }

            m_gamePhase.Value = GamePhase.RespawnWait;

            m_isWaitingRespawn = true;

            m_waitingRespawnPlayer.Value = _deadPlayer;

            m_respawnConfirmed = false;

            m_respawnBlockNetworkId = 0;

            if (m_debugMode)
            {
                Debug.Log("Waiting for Respawn. Player: " + _deadPlayer.OwnerClientId);
            }

            List<SpawnPoint> _spawnPointsForPlayer = GetRespawnPoints(_deadPlayer);

            if (_spawnPointsForPlayer == null ||
                _spawnPointsForPlayer.Count == 0)
            {
                Debug.LogWarning("No valid respawn points for player: " + _deadPlayer.OwnerClientId);

                _deadPlayer.m_lifeCurrent.Value = -1;

                m_waitingRespawnPlayer.Value = default;

                m_isWaitingRespawn = false;

                yield break;
            }

            m_timer.Value = 0f;

            while (m_timer.Value < m_respawnWaitTime)
            {
                if (m_respawnConfirmed)
                {
                    break;
                }

                m_timer.Value += Time.deltaTime;

                yield return null;
            }

            SpawnPoint _selectedSpawnPoint =
                null;

            if (m_respawnBlockNetworkId != 0)
            {
                BlockController _selectedBlock = GetBlockById(m_respawnBlockNetworkId);

                if (IsValidRespawnBlock(_deadPlayer, _selectedBlock))
                {
                    _selectedSpawnPoint = _selectedBlock.GetSpawn();
                }
            }

            if (_selectedSpawnPoint == null)
            {
                _spawnPointsForPlayer = GetRespawnPoints(_deadPlayer);

                if (_spawnPointsForPlayer == null || _spawnPointsForPlayer.Count == 0)
                {
                    Debug.LogWarning("No valid respawn points after wait. Player: " + _deadPlayer.OwnerClientId);

                    _deadPlayer.m_lifeCurrent.Value = -1;

                    m_waitingRespawnPlayer.Value = default;

                    m_isWaitingRespawn = false;

                    yield break;
                }

                _selectedSpawnPoint = _spawnPointsForPlayer[Random.Range(0, _spawnPointsForPlayer.Count)];
            }

            if (_selectedSpawnPoint == null)
            {
                Debug.LogWarning("Selected respawn point is null.");

                _deadPlayer.m_lifeCurrent.Value = -1;

                m_waitingRespawnPlayer.Value = default;

                m_isWaitingRespawn = false;

                yield break;
            }

            _deadPlayer.SetSpawnPoint(_selectedSpawnPoint);

            RespawnCharacter(_deadPlayer);

            m_waitingRespawnPlayer.Value = default;

            m_respawnBlockNetworkId = 0;

            m_respawnConfirmed = false;

            m_isWaitingRespawn = false;
        }

        m_gamePhase.Value = GamePhase.ResolveCards;
    }
    /// <summary>
    /// Checks if the specified player is currently waiting to respawn. It verifies that the game phase is RespawnWait and that the waiting respawn player matches the provided player session.
    /// </summary>
    /// <param name="player">The player session to check.</param>
    /// <returns>True if the player is waiting to respawn; otherwise, false.</returns>
    public bool IsWaitingRespawnPlayer(PlayerSession player)
    {
        if (player == null)
            return false;

        if (m_gamePhase.Value != GamePhase.RespawnWait)
            return false;

        if (!m_waitingRespawnPlayer.Value.TryGet(
                out PlayerSession _waitingPlayer))
        {
            return false;
        }

        return _waitingPlayer == player;
    }
    /// <summary>
    /// Searches for any player who has lost all their lives and returns the corresponding PlayerSession. It iterates through the list of players and checks their current life value, returning the first player found with zero or less life.
    /// </summary>
    /// <returns>True if any player is dead; otherwise, false.</returns>
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
    /// <summary>
    /// Checks if the game is finished by evaluating the number of players who are alive or can respawn. It counts the players who have a character or have remaining lives and determines if only one or fewer players are left, indicating that the game is finished.
    /// </summary>
    /// <returns>True if the game is finished; otherwise, false.</returns>
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
        FindLostPlayer();
        return _result;
    }
    /// <summary>
    /// Checks if all players have confirmed their card selections for the current turn. It iterates through the turn selections and counts the number of players who have confirmed their selections, returning true if all players have confirmed.
    /// </summary>
    /// <returns>True if all players have confirmed their selections; otherwise, false.</returns>
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
    /// <summary>
    /// Builds the turn order for the current round based on the total cost of the cards selected by each player. It clears the existing turn order and sorts the players in descending order of their total card costs, updating the turn order list accordingly.
    /// </summary>
    private void BuildTurnOrderByCardCost()
    {
        m_turnOrder.Clear();

        List<PlayerSession> _turnOrder = m_turnSelections.OrderByDescending(kvp => kvp.Value.TotalCost()).Select(kvp => kvp.Key).ToList();
        m_turnOrder = _turnOrder;
    }
    /// <summary>
    /// Finds a player who is currently dead (i.e., has no character) and has remaining lives. It iterates through the list of players and returns the first player found who meets these criteria, or null if no such player exists.
    /// </summary>
    /// <returns>The first dead player with remaining lives, or null if no such player exists.</returns>
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
}
