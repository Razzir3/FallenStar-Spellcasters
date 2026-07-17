using System.Collections.Generic;

[System.Serializable]
public class PlayerTurnSelection
{
    public PlayerSession m_player;

    public List<NetworkCard> m_cardsToPlay = new();
    public List<NetworkCard> m_cardToDiscard = new();

    public bool m_isConfirmed;

    public int TotalCost()
    {
        int _result = 0;

        foreach (NetworkCard card in m_cardsToPlay)
        {
            if (card != null)
            {
                _result += card.m_cardData.m_cost;
            }
        }
        foreach (NetworkCard card in m_cardToDiscard)
        {
            if (card != null)
            {
                _result += card.m_cardData.m_cost;
            }
        }
        return _result;
    }

    public void Clear()
    {
        m_cardsToPlay.Clear();
        m_cardToDiscard.Clear();
        m_isConfirmed = false;
    }
}
