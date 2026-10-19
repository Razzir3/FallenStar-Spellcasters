using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public partial class GameMaster
{
    /// <summary>
    /// Finds a NetworkCard by its network object ID.
    /// </summary>
    /// <param name="networkObjectId">The network object ID of the card to find.</param>
    /// <returns>The NetworkCard with the specified network object ID, or null if not found.</returns>
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
    /// <summary>
    /// Returns the CardDatabase instance used by the GameMaster.
    /// </summary>
    /// <returns>The CardDatabase instance.</returns>
    public CardDatabase GetCardDatabase() { return m_cardDatabase; }
    /// <summary>
    /// Moves a card to the graveyard, removing it from the owner's hand, board play, and board discard lists.
    /// </summary>
    /// <param name="card">The card to move to the graveyard.</param>
    private void MoveCardOnGraveyard(NetworkCard card)
    {
        if (!IsServer)
            return;

        if (card == null)
            return;

        ulong _cardNetworkObjectId = card.NetworkObjectId;

        PlayerSession _owner = GetSessionByID(card.m_ownerId.Value);

        if (_owner != null)
        {
            RemoveNetworkObjectId(_owner.m_handCards,_cardNetworkObjectId);

            RemoveNetworkObjectId(_owner.m_boardPlayCards,_cardNetworkObjectId);

            RemoveNetworkObjectId(_owner.m_boardDiscardCards,_cardNetworkObjectId);
        }
        else
        {
            Debug.LogWarning(
                $"Owner was not found for card " +
                $"{_cardNetworkObjectId}. Owner ID: " +
                $"{card.m_ownerId.Value}");
        }

        card.SetLocation(CardLocation.Graveyard);

        if (!m_graveyard.Contains(card))
        {
            m_graveyard.Add(card);
        }

        if (m_debugMode)
        {
            Debug.Log($"Card {_cardNetworkObjectId} moved to graveyard.");
        }
    }
    /// <summary>
    /// Clears the discarded cards for a given player, moving them to the graveyard.
    /// </summary>
    /// <param name="player">The player whose discarded cards are to be cleared.</param>
    private void ClearDiscardedCards(PlayerSession player)
    {
        if (!IsServer)
            return;

        if (player == null)
            return;

        List<NetworkCard> _cardsToRemove = new List<NetworkCard>();

        for (int i = 0; i < player.m_boardDiscardCards.Count; i++)
        {
            ulong _cardNetworkObjectId = player.m_boardDiscardCards[i];

            NetworkCard _card = GetCardByID(_cardNetworkObjectId);

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
    /// <summary>
    /// Clears the played cards for a given player, moving them to the graveyard.
    /// </summary>
    /// <param name="player">The player whose played cards are to be cleared.</param>
    private void ClearPlayedCards(PlayerSession player)
    {
        if (!IsServer)
            return;

        if (player == null)
            return;

        List<NetworkCard> _cardsToRemove = new List<NetworkCard>();

        for (int i = 0; i < player.m_boardPlayCards.Count; i++)
        {
            ulong _cardNetworkObjectId = player.m_boardPlayCards[i];

            NetworkCard _card = GetCardByID(_cardNetworkObjectId);

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
    /// <summary>
    /// Draws a card from the deck for the specified player session, adds it to the player's hand, and sends the card data to the client.
    /// </summary>
    /// <param name="session">The player session for which to draw a card.</param>
    private void DrawCard(PlayerSession session)
    {
        if (session == null)
        {
            Debug.LogWarning("Cannot draw card: player session is null.");
            return;
        }

        while (m_deck.Count > 0 && m_deck[0] == null)
            m_deck.RemoveAt(0);

        if (m_deck.Count == 0)
        {
            RefillDeck();

            while (m_deck.Count > 0 && m_deck[0] == null)
                m_deck.RemoveAt(0);
        }

        if (m_deck.Count == 0)
        {
            Debug.LogWarning("No card to draw");
            return;
        }

        var card = m_deck[0];

        session.AddCard(card);

        card.SetOwner(session.NetworkObjectId);

        SendCardDataClientRpc(card.NetworkObjectId, card.m_cardData.m_cardId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { session.OwnerClientId } } });

        m_deck.Remove(card);

        DiscardOldestOverHandLimit(session);

        if(m_debugMode)
            Debug.Log("Draw");
    }
    private bool IsCardCommitted(PlayerSession session, ulong cardId)
    {
        return ContainsNetworkObjectId(session.m_boardPlayCards, cardId) ||
            ContainsNetworkObjectId(session.m_boardDiscardCards, cardId);
    }

    private void DiscardOldestOverHandLimit(PlayerSession session)
    {
        if (!IsServer || m_maxHandCards <= 0)
            return;

        List<ulong> _uncommitted = new List<ulong>();
        foreach (ulong _cardId in session.m_handCards)
        {
            if (!IsCardCommitted(session, _cardId))
                _uncommitted.Add(_cardId);
        }

        int _excess = _uncommitted.Count - m_maxHandCards;
        if (_excess <= 0)
            return;

        int _count = 0;
        for (int i = 0; i < _excess; i++)
        {
            NetworkCard _card = GetCardByID(_uncommitted[i]);
            if (_card == null)
                RemoveNetworkObjectId(session.m_handCards, _uncommitted[i]);
            else
                MoveCardOnGraveyard(_card);
            _count++;
        }

        if (_count > 0)
        {
            if (m_debugMode)
                Debug.Log($"Hand limit {m_maxHandCards}: discarded {_count} oldest card(s). Client {session.OwnerClientId}");

            session.HandLimitDiscardClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { session.OwnerClientId } }
            });
        }
    }

    /// <summary>
    /// Deletes the old graveyard before creating a new deck with the configured rarity limit.
    /// </summary>
    private void RefillDeck()
    {
        if (!IsServer)
            return;

        foreach (NetworkCard card in m_graveyard)
        {
            if (card == null)
                continue;

            if (card.NetworkObject != null && card.NetworkObject.IsSpawned)
                card.NetworkObject.Despawn(true);
            else
                Destroy(card.gameObject);

            m_spawnedCards.Remove(card);
        }

        m_graveyard.Clear();

        CreateDeck(m_refillDeckMaxRarity);

        if (m_debugMode)
            Debug.Log($"Deck refilled: {m_deck.Count} cards.");

        if (m_deck.Count == 0)
            Debug.LogWarning("Refill deck is empty.");
    }
    /// <summary>
    /// Creates a deck of cards by randomly selecting cards from the card database based on specified maximum counts for different card types (Attack, Defence, Draw Card). The deck is then shuffled.
    /// </summary>
    private void CreateDeck(CardRarity maxRarity = CardRarity.Unique)
    {
        m_deck.Clear();
        int _randomSeed = Random.Range(0, 2);
        int _maxAttackSpell = 0;
        int _maxDefenceSpell = 0;
        int _maxDrawCardSpell = 0;

        int _currentAttackSpell = 0;
        int _currentDefenceSpell = 0;
        int _currentDrawCardSpell = 0;
        switch (_randomSeed)
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

        foreach (var card in m_cardDatabase.m_cards)
        {
            if (card == null || card.m_rarity > maxRarity)
                continue;

            switch (card.m_specialSpecialization)
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
        FillCardSection(_maxDefenceSpell, _currentDefenceSpell, _allDefenceSpell, _uniqueDefenceSpell);
        FillCardSection(_maxDrawCardSpell, _currentDrawCardSpell, _allDrawCardSpell, _uniqueDrawCardSpell);

        m_deck.Shuffle();
    }
    /// <summary>
    /// Fills a section of the deck with cards of a specific type, ensuring that the maximum number of cards for that type is not exceeded. It randomly selects cards from the unique list first, and if there are no unique cards left, it selects from the non-unique list. The selected cards are instantiated as NetworkCard objects and added to the deck.
    /// </summary>
    /// <param name="maxCardType">The maximum number of cards of this type allowed in the deck.</param>
    /// <param name="currentCardType">The current number of cards of this type in the deck.</param>
    /// <param name="allList">The list of all available cards of this type.</param>
    /// <param name="uniqueList">The list of unique cards of this type.</param>
    private void FillCardSection(int maxCardType, int currentCardType, List<CardData> allList, List<CardData> uniqueList)
    {
        int _randomSeed;
        List<CardData> _nonUniqueList = new List<CardData>();
        while (maxCardType > currentCardType)
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
}
