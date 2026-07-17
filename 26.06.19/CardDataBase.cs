using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class CardDatabase : ScriptableObject
{
    public List<CardData> m_cards;

    private Dictionary<string, CardData> m_cardsById;

    public IReadOnlyList<CardData> Cards => m_cards;

    public void Initialize()
    {
        if (m_cardsById != null)
            return;

        m_cardsById = new Dictionary<string, CardData>();

        foreach (CardData card in m_cards)
        {
            if (card == null)
                continue;

            if (string.IsNullOrWhiteSpace(card.m_cardId))
            {
                Debug.LogError(
                    $"CardData '{card.name}' has empty card ID.",
                    card
                );

                continue;
            }

            if (!m_cardsById.TryAdd(card.m_cardId, card))
            {
                Debug.LogError(
                    $"Duplicate CardData ID: {card.m_cardId}",
                    card
                );
            }
        }
    }

    public bool TryGetCard(
        string cardId,
        out CardData cardData)
    {
        Initialize();

        return m_cardsById.TryGetValue(
            cardId,
            out cardData
        );
    }

    public CardData GetCard(string cardId)
    {
        Initialize();

        if (m_cardsById.TryGetValue(cardId, out CardData card))
            return card;

        Debug.LogError($"CardData not found: {cardId}");
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        HashSet<string> ids = new();

        foreach (CardData card in m_cards)
        {
            if (card == null)
                continue;

            if (string.IsNullOrWhiteSpace(card.m_cardId))
            {
                Debug.LogError(
                    $"CardData '{card.name}' has empty ID.",
                    card
                );

                continue;
            }

            if (!ids.Add(card.m_cardId))
            {
                Debug.LogError(
                    $"Duplicate CardData ID: {card.m_cardId}",
                    card
                );
            }
        }

        m_cardsById = null;
    }
#endif
}