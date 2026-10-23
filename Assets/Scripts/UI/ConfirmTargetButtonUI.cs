using Unity.Netcode;
using UnityEngine;

public class ConfirmTargetButtonUI : MonoBehaviour
{
    private PlayerSession m_localSession;
    public void ConfirmSelect()
    {
        if (m_localSession == null)
            m_localSession = SessionManager.Instance != null ? SessionManager.Instance.LocalPlayer : null;
        if (m_localSession == null)
        {
            Debug.LogWarning("ConfirmSelect ignored: local PlayerSession is null.");
            return;
        }
        m_localSession.TryConfirmTarget();
    }
}
