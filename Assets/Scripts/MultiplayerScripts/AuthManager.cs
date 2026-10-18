using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    #region Variable
    public static AuthManager Instance;

    public bool m_debugMode = false;

    private bool m_isInitializing;
    private bool m_isSingingIn;
    

    public bool IsSignedIn
    {
        get
        {
            return AuthenticationService.Instance.IsSignedIn;
        }
    }

    public string PlayerId
    {
        get
        {
            return AuthenticationService.Instance.PlayerId;
        }
    }
    #endregion

    #region UnityCallingFunc
    private void Awake()
    {
        if(Instance !=null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Task
    public async Task Initialize()
    {
        if (m_isInitializing)
            return;

        m_isInitializing = true;

        try
        {
            if(UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
        }
        finally
        {
            m_isInitializing = false;
        }
    }
    public async Task SingInAnonymous()
    {
        if (m_isSingingIn)
            return;

        m_isSingingIn = true;

        try
        {
            await Initialize();

            ApplyEditorProfile();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (m_debugMode)
            {
                Debug.Log("Signed in. PlayerId: " + AuthenticationService.Instance.PlayerId);
            }
        }
        catch (AuthenticationException e)
        {
            Debug.LogError(e);
        }
        catch (RequestFailedException e)
        {
            Debug.LogError(e);
        }
        finally
        {
            m_isSingingIn = false;
        }
    }

    public async Task SignInSteam()
    {
        await Initialize();

        Debug.LogWarning("Steam login is not ready. Use SingInAnonymous");
    }
    #endregion

    #region SingOutFunc
    public void SingOut()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            return;

        AuthenticationService.Instance.SignOut();
    }
    public void SingOutAndClearCredentials()
    {
        if(!AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut(true);
        }
        else
        {
            AuthenticationService.Instance.ClearSessionToken();
        }
    }
    #endregion
    private void ApplyEditorProfile()
    {
#if UNITY_EDITOR
        if (Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            return;

        string _profileName =
            "Editor_" +
            Mathf.Abs(Application.dataPath.GetHashCode());

        Unity.Services.Authentication.AuthenticationService.Instance
            .SwitchProfile(_profileName);

        Debug.Log(
            "Auth editor profile selected: " +
            _profileName);
#endif
    }
}