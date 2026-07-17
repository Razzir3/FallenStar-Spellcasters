using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsSceneButtonsUI : MonoBehaviour
{
    public void OnClickBack()
    {
        if(SceneFlowManager.Instance == null)
        {
            Debug.LogError("SettingsSceneButtonsUI: SceneFlow is null");
            return;
        }
        SceneFlowManager.Instance.LoadMainMenuScene();
    }
}
