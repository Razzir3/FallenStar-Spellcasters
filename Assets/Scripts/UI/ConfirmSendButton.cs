using UnityEngine;

public class ConfirmSendButtonUI1 : MonoBehaviour
{
    private PlayerSession m_localSession;
    public void TryConfirmSend()
    {
        if (m_localSession == null)
            m_localSession = SessionManager.Instance.LocalPlayer;
        m_localSession.TryHandlePackage();
    }
}
