using UnityEngine;

public class ResultSceneController : MonoBehaviour
{
    private void Awake()
    {
        m_debugMode = DebugModeGuard.Allow(m_debugMode);
    }

    [SerializeField] private bool m_debugMode = false;

    private void Start()
    {
        if (!m_debugMode)
            return;

        if (!MatchResultData.HasResult)
        {
            Debug.LogWarning("ResultScene: no match result available.");
            return;
        }

        Debug.Log($"ResultScene: {(MatchResultData.IsDraw ? "Draw" : MatchResultData.IsWinner ? "You won" : "You lost")}. Reason: {MatchResultData.Reason}");
    }
}
