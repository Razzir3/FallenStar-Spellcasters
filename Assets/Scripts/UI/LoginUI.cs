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

        bool _signedIn = false;
        try
        {
            await AuthManager.Instance.SingInAnonymous();
            _signedIn = AuthManager.Instance.IsSignedIn;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        if(_signedIn)
        {
            SceneFlowManager.Instance.LoadMainMenuScene();
        }
        else
        {
            PlayerNotice.Post("notice_sign_in_failed");
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
