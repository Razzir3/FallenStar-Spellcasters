using UnityEngine;

public class DisconnectButtonUI : MonoBehaviour
{
    public void OnClickDisconnect()
    {
        if(MultiplayerManager.Instance == null)
        {
            Debug.LogError("DisconnectButton: Multiplayer Manager is null");
            return;
        }

        MultiplayerManager.Instance.Disconnect();
    }
}
