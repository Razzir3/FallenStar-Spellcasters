using UnityEngine;
using UnityEngine.UI;

public class LeaveMatchUI : MonoBehaviour
{
    [SerializeField] private Button m_leaveButton;
    [SerializeField] private GameObject m_confirmPanel;
    [SerializeField] private float m_leaveFallbackTimeout = 5f;
    [SerializeField] private bool m_debugMode;

    private bool m_isLeaving;
    private float m_leaveDeadline = -1f;
    private RectTransform m_layoutRect;
    private RectTransform m_canvasRect;

    private void Start()
    {
        m_layoutRect = transform as RectTransform;
        Canvas _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            m_canvasRect = _canvas.rootCanvas.transform as RectTransform;
        HideConfirmPanel();
    }

    private void Update()
    {
        if (m_layoutRect != null && m_canvasRect != null && m_layoutRect != m_canvasRect &&
            m_layoutRect.sizeDelta != m_canvasRect.rect.size)
            m_layoutRect.sizeDelta = m_canvasRect.rect.size;

        bool _isGameOver = GameMaster.Instance != null &&
            GameMaster.Instance.m_gamePhase.Value == GamePhase.GameOver;

        if (m_leaveButton != null)
            m_leaveButton.interactable = GameMaster.Instance != null && !_isGameOver && !m_isLeaving;

        if (_isGameOver)
        {
            HideConfirmPanel();
            m_leaveDeadline = -1f;
        }
        else if (m_leaveDeadline >= 0f && Time.unscaledTime >= m_leaveDeadline)
        {
            m_leaveDeadline = -1f;
            Disconnect();
        }
    }

    public void OnClickLeave()
    {
        if (m_isLeaving || GameMaster.Instance == null ||
            GameMaster.Instance.m_gamePhase.Value == GamePhase.GameOver)
            return;

        if (m_confirmPanel != null)
            m_confirmPanel.SetActive(true);
    }

    public void OnClickCancelLeave()
    {
        HideConfirmPanel();
    }

    public void OnClickConfirmLeave()
    {
        if (m_isLeaving || (GameMaster.Instance != null &&
            GameMaster.Instance.m_gamePhase.Value == GamePhase.GameOver))
            return;

        m_isLeaving = true;
        HideConfirmPanel();

        if (GameMaster.Instance != null &&
            GameMaster.Instance.m_gamePhase.Value != GamePhase.None &&
            SessionManager.Instance != null && SessionManager.Instance.LocalPlayer != null)
        {
            m_leaveDeadline = Time.unscaledTime + m_leaveFallbackTimeout;
            SessionManager.Instance.LocalPlayer.RequestLeaveMatch();
        }
        else
        {
            Disconnect();
        }
    }

    private void HideConfirmPanel()
    {
        if (m_confirmPanel != null)
            m_confirmPanel.SetActive(false);
    }

    private void Disconnect()
    {
        if (MultiplayerManager.Instance == null)
        {
            Debug.LogError("LeaveMatchUI: MultiplayerManager is null.");
            return;
        }

        if (m_debugMode)
            Debug.Log("LeaveMatchUI: disconnecting before start or after surrender timeout.");

        MultiplayerManager.Instance.Disconnect();
    }
}
