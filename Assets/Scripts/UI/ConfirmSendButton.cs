using UnityEngine;

public class ConfirmSendButtonUI1 : MonoBehaviour
{
    private PlayerSession m_localSession;
    public void TryConfirmSend()
    {
        if (m_localSession == null)
            m_localSession = SessionManager.Instance != null ? SessionManager.Instance.LocalPlayer : null;
        if (m_localSession == null)
        {
            Debug.LogWarning("TryConfirmSend ignored: local PlayerSession is null.");
            return;
        }
        m_localSession.TryHandlePackage();
    }
}
