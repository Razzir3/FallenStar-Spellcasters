using UnityEngine;

public class PlayerCardWorldArea : MonoBehaviour
{
    #region Variable

    [Header("Session Binding")]

    [Tooltip("„H„~„p„‰„u„~„y„u „t„€„|„w„~„€ „ƒ„€„€„„„r„u„„„ƒ„„„r„€„r„p„„„ PlayerSession.TeamId.Value")]
    [SerializeField]
    public int m_sessionSearchId;

    [SerializeField]
    private PlayerSession m_playerSession;

    [Header("Card Positioning")]

    [Tooltip("„C„€„‚„y„x„€„~„„„p„|„„~„€„u „‚„p„ƒ„ƒ„„„€„‘„~„y„u „}„u„w„t„… „ˆ„u„~„„„‚„p„}„y „ƒ„€„ƒ„u„t„~„y„‡ „{„p„‚„„")]
    [SerializeField]
    private float m_cardInterval = 2f;

    #endregion

    #region Getter

    public PlayerSession GetPlayerSession()
    {
        return m_playerSession;
    }

    public ulong GetSessionNetworkObjectId()
    {
        if (m_playerSession == null)
            return 0;

        if (!m_playerSession.IsSpawned)
            return 0;

        return m_playerSession.NetworkObjectId;
    }

    public float GetCardInterval()
    {
        return m_cardInterval;
    }

    /*
     * „B„€„x„r„‚„p„‹„p„u„„ „|„€„{„p„|„„~„…„ „„€„x„y„ˆ„y„ „{„p„‚„„„ „€„„„~„€„ƒ„y„„„u„|„„~„€
     * „ˆ„u„~„„„‚„p PlayerCardWorldArea.
     *
     * „C„u„€„}„u„„„‚„y„‰„u„ƒ„{„y„z „ˆ„u„~„„„‚ „r„ƒ„u„z „„€„ƒ„|„u„t„€„r„p„„„u„|„„~„€„ƒ„„„y
     * „r„ƒ„u„s„t„p „q„…„t„u„„ „~„p„‡„€„t„y„„„„ƒ„‘ „r localPosition = Vector3.zero.
     */
    public Vector3 GetCardLocalPosition(
        int index,
        int cardCount)
    {
        if (cardCount <= 0)
            return Vector3.zero;

        if (index < 0 || index >= cardCount)
        {
            Debug.LogWarning(
                $"PlayerCardWorldArea {name}: " +
                $"invalid card index {index}. " +
                $"Card count: {cardCount}.");

            return Vector3.zero;
        }

        float centerIndex =
            (cardCount - 1) * 0.5f;

        float horizontalPosition =
            (index - centerIndex) * m_cardInterval;

        /*
         * „I„ƒ„„€„|„„x„…„u„„„ƒ„‘ „|„€„{„p„|„„~„p„‘ „€„ƒ„ X „€„q„Œ„u„{„„„p.
         * „P„€„r„€„‚„€„„ PlayerCardWorldArea „p„r„„„€„}„p„„„y„‰„u„ƒ„{„y
         * „„€„r„€„‚„p„‰„y„r„p„u„„ „r„ƒ„ „„€„ƒ„|„u„t„€„r„p„„„u„|„„~„€„ƒ„„„ „~„p „ƒ„ˆ„u„~„u.
         */
        return new Vector3(
            horizontalPosition,
            0f,
            0f);
    }

    #endregion

    #region Initialize

    public void Initialize(PlayerSession playerSession)
    {
        if (playerSession == null)
        {
            Debug.LogError(
                $"PlayerCardWorldArea {name}: " +
                $"attempt to initialize with null PlayerSession.");

            return;
        }

        m_playerSession = playerSession;

        if (CardWorldViewManager.Instance != null)
        {
            CardWorldViewManager.Instance.RegisterArea(this);
        }
    }

    public void ClearPlayerSession()
    {
        m_playerSession = null;
    }

    #endregion

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        /*
         * „P„€„{„p„x„„r„p„u„„ „ˆ„u„~„„„‚ PlayerCardWorldArea.
         */
        Gizmos.DrawWireSphere(
            Vector3.zero,
            0.15f);

        /*
         * „P„€„{„p„x„„r„p„u„„ „s„€„‚„y„x„€„~„„„p„|„„~„€„u „~„p„„‚„p„r„|„u„~„y„u,
         * „r„t„€„|„ „{„€„„„€„‚„€„s„€ „q„…„t„…„„ „‚„p„x„}„u„‹„p„„„„ƒ„‘ „{„p„‚„„„.
         */
        Gizmos.DrawLine(
            Vector3.left * m_cardInterval * 3f,
            Vector3.right * m_cardInterval * 3f);
    }

#endif
}