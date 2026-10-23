using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class Bootstrap : MonoBehaviour
{
    #region Variable

    [Header("Prefab list")]
    [SerializeField]
    private GameObject m_networkManagerPrefab;

    [SerializeField]
    private GameObject m_gameMasterPrefab;

    public GameObject m_gameMasterInstance;
    public GameObject m_networkManagerInstance;

    #endregion

    #region UnityFunc
    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        ApplySavedLanguage();
    }
    #endregion

    #region Localization
    private void ApplySavedLanguage()
    {
        string _savedCode = GameSettingsPrefs.LanguageCode;

        if(string.IsNullOrEmpty(_savedCode))
        {
            Debug.LogWarning(
                "Bootstrap: No saved language code found.");
            return;
        }

        Locale _locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(_savedCode));

        if(_locale == null)
        {
            Debug.LogWarning(
                $"Bootstrap: Locale with code '{_savedCode}' not found.");
            return;
        }

        LocalizationSettings.SelectedLocale = _locale;
    }
    #endregion

    #region NetworkManager

    public void CreateNetworkManagerInstance()
    {
        if (NetworkManager.Singleton != null)
        {
            m_networkManagerInstance = NetworkManager.Singleton.gameObject;

            DontDestroyOnLoad(m_networkManagerInstance);

            return;
        }

        if (m_networkManagerPrefab == null)
        {
            Debug.LogError("Bootstrap: m_networkManagerPrefab is null.");

            return;
        }

        m_networkManagerInstance = Instantiate(m_networkManagerPrefab);

        DontDestroyOnLoad (m_networkManagerInstance);
    }

    public async Task RecreateNetworkManager()
    {
        NetworkManager oldNetworkManager =
            NetworkManager.Singleton;

        if (oldNetworkManager != null)
        {
            if (oldNetworkManager.IsListening)
            {
                oldNetworkManager.Shutdown();
            }

            Destroy(oldNetworkManager.gameObject);

            while (NetworkManager.Singleton != null)
            {
                await Task.Yield();
            }
        }

        CreateNetworkManagerInstance();

        while (NetworkManager.Singleton == null)
        {
            await Task.Yield();
        }

        m_networkManagerInstance =
            NetworkManager.Singleton.gameObject;

    }

    #endregion

    #region GameMaster

    public void CreateGameMasterInstance()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "No network manager(createGM)");

            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning(
                "CreateGameMasterInstance ignored: only server can create GameMaster.");

            return;
        }

        if (GameMaster.Instance != null)
        {
            m_gameMasterInstance =
                GameMaster.Instance.gameObject;

            return;
        }

        m_gameMasterInstance =
            Instantiate(m_gameMasterPrefab);

        NetworkObject _networkObject =
            m_gameMasterInstance.GetComponent<NetworkObject>();

        if (_networkObject == null)
        {
            Debug.LogError(
                "GameMaster prefab has no NetworkObject.");

            Destroy(m_gameMasterInstance);
            m_gameMasterInstance = null;

            return;
        }

        _networkObject.Spawn();
    }

    public async Task RecreateGameMaster()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "RecreateGameMaster: NetworkManager.Singleton is null.");

            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning(
                "RecreateGameMaster ignored: only server can recreate GameMaster.");

            return;
        }

        if (GameMaster.Instance != null)
        {
            NetworkObject _networkObject =
                GameMaster.Instance.NetworkObject;

            if (_networkObject != null &&
                _networkObject.IsSpawned)
            {
                _networkObject.Despawn(true);
            }
            else
            {
                Destroy(GameMaster.Instance.gameObject);
            }

            while (GameMaster.Instance != null)
            {
                await Task.Yield();
            }
        }

        CreateGameMasterInstance();
    }

    #endregion
}