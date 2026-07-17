using Unity.Netcode;
using UnityEngine;

public class ConfirmTargetButtonUI : MonoBehaviour
{
    private PlayerSession m_localSession;
    public void ConfirmSelect()
    {
        if (m_localSession == null)
            m_localSession = SessionManager.Instance.LocalPlayer;
        m_localSession.TryConfirmTarget();
    }
}
