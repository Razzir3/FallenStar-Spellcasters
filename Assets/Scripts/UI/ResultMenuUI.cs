using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

public class ResultMenuUI : MonoBehaviour
{
    [SerializeField] private LocalizeStringEvent m_titleText;
    [SerializeField] private LocalizeStringEvent m_reasonText;
    [SerializeField] private LocalizedString m_titleWin;
    [SerializeField] private LocalizedString m_titleLose;
    [SerializeField] private LocalizedString m_titleDraw;
    [SerializeField] private LocalizedString m_reasonYouLostLives;
    [SerializeField] private LocalizedString m_reasonOpponentLostLives;
    [SerializeField] private LocalizedString m_reasonBothLostLives;
    [SerializeField] private LocalizedString m_reasonOpponentLeft;
    [SerializeField] private bool m_debugMode = false;

    private bool m_isLeaving;

    private void Start()
    {
        if (!MatchResultData.HasResult)
        {
            m_titleText.gameObject.SetActive(false);
            m_reasonText.gameObject.SetActive(false);

            if (m_debugMode)
                Debug.Log("ResultMenuUI: no match result available.");

            return;
        }

        m_titleText.StringReference = MatchResultData.IsDraw
            ? m_titleDraw
            : MatchResultData.IsWinner ? m_titleWin : m_titleLose;

        switch (MatchResultData.Reason)
        {
            case MatchEndReason.LivesDepleted:
                m_reasonText.StringReference = MatchResultData.IsDraw
                    ? m_reasonBothLostLives
                    : MatchResultData.IsWinner ? m_reasonOpponentLostLives : m_reasonYouLostLives;
                break;
            case MatchEndReason.OpponentDisconnected:
                m_reasonText.StringReference = m_reasonOpponentLeft;
                break;
        }

        m_titleText.RefreshString();
        m_reasonText.RefreshString();
    }

    public void OnClickMainMenu()
    {
        if (m_isLeaving)
            return;

        MatchResultData.Clear();

        if (SceneFlowManager.Instance == null)
        {
            Debug.LogError("ResultMenuUI: SceneFlow is null");
            return;
        }

        m_isLeaving = true;
        SceneFlowManager.Instance.LoadMainMenuScene();
    }
}
