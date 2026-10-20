using Unity.Netcode;
using UnityEngine;
public enum CardLocation
{
    Hand,
    Board,
    Deck,
    Graveyard
}
public class NetworkCard : NetworkBehaviour
{
    #region Variable
    [Header("Card Data")]
    public CardData m_cardData;

    [Header("Prefabs")]
    [SerializeField]
    private CardViewUI m_uiPrefab;

    [SerializeField]
    private CardViewWorld m_worldPrefab;

    [Header("State")]
    public NetworkVariable<bool> m_isFaceUp = new NetworkVariable<bool>(true);

    public NetworkVariable<bool> m_isRotated = new NetworkVariable<bool>(false);

    public NetworkVariable<ulong> m_ownerId = new NetworkVariable<ulong>(0);

    public NetworkVariable<CardLocation> m_location = new NetworkVariable<CardLocation>(CardLocation.Deck);

    public bool m_isSelected;

    private CardViewBase m_currentView;
    private CardViewBase m_additionalView;
    #endregion

    #region OnSpawnFunc
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        m_location.OnValueChanged +=
            OnLocationChanged;

        m_isRotated.OnValueChanged +=
            OnRotatedChanged;

        m_isFaceUp.OnValueChanged +=
            OnFaceUpChanged;
    }
    public override void OnNetworkDespawn()
    {
        m_location.OnValueChanged -=
            OnLocationChanged;

        m_isRotated.OnValueChanged -=
            OnRotatedChanged;

        m_isFaceUp.OnValueChanged -=
            OnFaceUpChanged;
        ClearView();
        base.OnNetworkDespawn();
    }
    #endregion

    #region ViewUpdates
    #endregion


    #region Rpc
    [ServerRpc]
    public void SetFaceUpServerRpc(bool value)
    {
        m_isFaceUp.Value = value;
    }
    [ServerRpc]
    public void SetRotatedServerRpc(bool value)
    {
        m_isRotated.Value = value;
    }
    [ServerRpc]
    public void DespawnCardServerRpc()
    {
        DespawnCard();
    }
    #endregion

    #region Setter
    public void SetOwner(ulong ownerId)
    {
        if (!IsServer)
            return;
        m_ownerId.Value = ownerId;
        m_location.Value = CardLocation.Hand;
    }

    public void SetLocation(CardLocation location)
    {
        if (!IsServer)
            return;
        m_location.Value = location;
    }

    public void SetCurrentView(CardViewBase view)
    {
        m_currentView = view;
    }
    public void SetAdditionalView(CardViewBase view)
    {
        m_additionalView = view;
    }
    #endregion

    #region ClearFunc
    private void ClearView()
    {
        if (m_currentView == null&&m_additionalView == null)
            return;
        if (m_currentView != null)
        {
            Destroy(m_currentView.gameObject);
        }
        if(m_additionalView!=null)
        {
            Destroy(m_additionalView.gameObject);
        }
        m_currentView = null;
        m_additionalView = null;
    }
    public void DespawnCard()
    {
        if (!IsServer)
            return;
        if(m_currentView != null)
        {
            Destroy(m_currentView.gameObject);
            m_currentView = null;
        }
        NetworkObject.Despawn(true);
    }

    public bool CheckActiveTaergetSelection()
    {
        if (m_cardData == null || m_cardData.m_activities == null)
            return false;

        for(int i = 0;i<m_cardData.m_activities.Length;i++)
        {
            if (m_cardData.m_activities[i].s_targetType == CardTargetType.AnyPlayer||
                m_cardData.m_activities[i].s_targetType == CardTargetType.AnyPlayerBlock||
                m_cardData.m_activities[i].s_targetType == CardTargetType.OpponentBlock||
                m_cardData.m_activities[i].s_targetType == CardTargetType.RandomBlockSelectedPlayer)
            {
                return true;
            }
        }
        return false;
    }
    #endregion

    public void RepairCardData(string cardId)
    {
        Debug.Log("RepairCalled");
        if (GameMaster.Instance == null)
        {
            Debug.LogWarning("RepairCardData: GameMaster.Instance is null.");
            return;
        }

        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("RepairCardData: card id is empty.");
            return;
        }

        var _cardDatabase = GameMaster.Instance.GetCardDatabase();
        if (_cardDatabase == null)
        {
            Debug.LogWarning("RepairCardData: card database is null.");
            return;
        }

        if (!_cardDatabase.TryGetCard(cardId, out CardData _data))
        {
            Debug.LogWarning($"RepairCardData: card id '{cardId}' was not found.");
            return;
        }

        m_cardData = _data;
    }
    private void OnLocationChanged(
    CardLocation previousValue,
    CardLocation newValue)
    {
        if (CardWorldViewManager.Instance != null)
        {
            CardWorldViewManager.Instance.RefreshAllSessions();
        }
    }

    private void OnRotatedChanged(
        bool previousValue,
        bool newValue)
    {
        /*
         * CardViewWorld уже может быть подписан напрямую,
         * но повторный Refresh гарантирует корректность
         * при позднем создании представления.
         */
        if (CardWorldViewManager.Instance != null)
        {
            CardWorldViewManager.Instance.RefreshAllSessions();
        }
    }

    private void OnFaceUpChanged(
        bool previousValue,
        bool newValue)
    {
        if (CardWorldViewManager.Instance != null)
        {
            CardWorldViewManager.Instance.RefreshAllSessions();
        }
    }
}
