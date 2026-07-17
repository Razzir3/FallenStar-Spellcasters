using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void OnClickLobby()
    {
        if(SceneFlowManager.Instance == null)
        {
            Debug.LogError("MainMenuUI: SceneFlow is null");
            return;
        }

        SceneFlowManager.Instance.LoadLobbyScene();
    }

    public void OnClickExit()
    {
        Application.Quit();
    }
}
