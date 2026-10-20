using UnityEngine;

public class PlayerCardWorldArea : MonoBehaviour
{
    #region Variable

    [Header("Session Binding")]

    [Tooltip("Значение должно соответствовать PlayerSession.TeamId.Value")]
    [SerializeField]
    public int m_sessionSearchId;

    [SerializeField]
    private PlayerSession m_playerSession;

    [Header("Card Positioning")]

    [Tooltip("Горизонтальное расстояние между центрами соседних карт")]
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
     * Возвращает локальную позицию карты относительно
     * центра PlayerCardWorldArea.
     *
     * Геометрический центр всей последовательности
     * всегда будет находиться в localPosition = Vector3.zero.
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
         * Используется локальная ось X объекта.
         * Поворот PlayerCardWorldArea автоматически
         * поворачивает всю последовательность на сцене.
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
         * Показывает центр PlayerCardWorldArea.
         */
        Gizmos.DrawWireSphere(
            Vector3.zero,
            0.15f);

        /*
         * Показывает горизонтальное направление,
         * вдоль которого будут размещаться карты.
         */
        Gizmos.DrawLine(
            Vector3.left * m_cardInterval * 3f,
            Vector3.right * m_cardInterval * 3f);
    }

#endif
}