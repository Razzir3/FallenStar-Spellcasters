using UnityEngine;

public class LoginUI : MonoBehaviour
{
    public async void OnClickLoginAnonymous()
    {
        if(AuthManager.Instance == null)
        {
            Debug.LogError("LoginUI: AuthManager is null");
            return;
        }

        await AuthManager.Instance.SingInAnonymous();

        if(AuthManager.Instance.IsSignedIn)
        {
            SceneFlowManager.Instance.LoadMainMenuScene();
        }
    }

    public async void OnClickLoginSteam()
    {
        if (AuthManager.Instance == null)
        {
            Debug.LogError("LoginUI: AuthManager is null");
            return;
        }

        await AuthManager.Instance.SignInSteam();

        if (AuthManager.Instance.IsSignedIn)
        {
            SceneFlowManager.Instance.LoadMainMenuScene();
        }
    }

    public void OnClickExit()
    {
        Application.Quit();
    }
}
