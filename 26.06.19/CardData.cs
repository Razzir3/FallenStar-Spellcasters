    using Unity.Services.Matchmaker.Models;
    using UnityEngine;
    public enum CardActionType
    {
        None = 0,
        //base(1-20)
        Damage = 1,
        Heal = 2,
        DrawCard = 3,
        DiscardCard = 4,
        //effect from drawn card(21-40)
        DrawCardDamage = 21,
        DrawCardHeal = 22,
        DrawCardDrawCard = 23,
        DrawCardDiscardCard = 24,
        //effect from discarded card(41-60)
        DiscardCardDamage = 41,
        DiscardCardHeal = 42,
        DiscardCardDrawCard = 43,
        DiscardCardDiscardCard = 44
    }
    public enum CardTargetType
    {
        None = 0,
        //Blocks(1-9)
        SelfBlock = 1,
        OpponentBlock = 2,
        AnyPlayerBlock = 3,
        RandomSelfBlock = 4,
        RandomOpponentBlock = 5,
        RandomBlockSelectedPlayer = 6,
        AllBlockSelf = 7,
        AllBlockOpponent = 8,
        AllBlockAnyPlayer = 9,
        //Players(21-30)
        SelfPlayer = 21,
        OpponentPlayer = 22,
        AnyPlayer = 23,
        AllPlayer = 29
    }

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        UltraRare,
        Unique
    }
    public enum CardSpecialization
    {
        AttackSpell,
        DeffenceSpell,
        CardDraftSpell
    }

    [System.Serializable]
    public struct  CardActivity
    {
        public CardActionType s_actionType;
        public CardTargetType s_targetType;
        public ActivityEffect s_effect;
        public int s_value;
    }

    public enum ActivityEffect
    {
        None,
        Fire1,
        Frost1
    }

    [CreateAssetMenu(fileName = "CardData", menuName = "ScriptableObjects/CardData", order = 1)]
    public class CardData : ScriptableObject
    {
        public string m_cardId;

        public Sprite m_artwork;
        public Sprite m_cardFrame;
        public Sprite m_cardBack;

        public string m_nameKey;
        public string m_descKey;

        public int m_cost;

        public CardRarity m_rarity;
        public CardSpecialization m_specialSpecialization;
    
        public CardActivity[] m_activities = new CardActivity[4];

        private void OnValidate()
        {
            if(m_activities == null || m_activities.Length != 4)
            {
                m_activities = new CardActivity[4];
            }

            for(int i = 1;i<=3;i++)
            {
                m_activities[i] = new CardActivity
                {
                    s_actionType = CardActionType.None,
                    s_targetType = CardTargetType.None,
                    s_value = 0,
                    s_effect = ActivityEffect.None
                };
            }
        }
    }
