
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.InputSystem;
public enum MultiplayerStatus
{
    InMenuStatus,
    InLobbyStatus,
    InGameStatus
}
public class MultiplayerManager : MonoBehaviour

{
    #region Valiable
    private Lobby m_currentLobby;
    private string m_joinCodeInput = "";

    public bool m_debugMode = false;

    private bool m_onDisconnecting = false;
    private bool m_onApplicationClosing = false;
    //ToDo Need full rework
    public TMP_Text m_lobbyCode;
    public GameObject m_lobbyCodePanel;

    private DisconnectMenuUI m_disconnectMenuUI;

    public static MultiplayerManager Instance;

    public MultiplayerStatus m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;

    [Header ("Manager Object")]
    [SerializeField] private GameObject m_gameMaster;

    [SerializeField] private GameObject m_sessionManager;

    [SerializeField] private Bootstrap m_bootsTrap;

    [SerializeField] private Canvas m_canvas;
    public bool m_isRelayAction = false;

    #endregion

    #region UnityCallingFunc
    async void Start()
    {
        Instance = this;

        m_canvas.gameObject.SetActive(false);

        await m_bootsTrap.RecreateNetworkManager();

        await InitUnityServices();

        InvokeRepeating(
            nameof(HearthbeatLobby),
            15f,
            15f);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback +=
                OnClientDisconnected;
        }

        m_canvas.gameObject.SetActive(true);
    }
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            if (m_debugMode)
            {
                Debug.Log("Client disconnect callback");
            }
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
        }
    }
    private async void OnApplicationQuit()
    {
        if (m_onApplicationClosing)
        { return; }
        m_onApplicationClosing = true;

        try
        {
            if (NetworkManager.Singleton != null)
            {
                if (m_currentLobby != null && NetworkManager.Singleton.IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(m_currentLobby.Id);
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e.Message);
        }
    }
    private void Update()
    {
        if (m_currentMultiplayerStatus == MultiplayerStatus.InLobbyStatus || m_currentMultiplayerStatus == MultiplayerStatus.InGameStatus)
        {
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                CheckRef();
                if (m_disconnectMenuUI != null)
                {
                    if (m_disconnectMenuUI.gameObject.activeSelf)
                    {
                        m_disconnectMenuUI.gameObject.SetActive(false);
                    }
                    else
                    {
                        m_disconnectMenuUI.gameObject.SetActive(true);
                    }
                }
            }
        }
    }
    #endregion

    #region ConnectControllFunc
    private void OnClientDisconnected(ulong clientId)
    {
        if (m_debugMode)
        {
            Debug.Log("Client disconnected: " + clientId);
        }

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (m_debugMode)
            {
                Debug.Log("Local client disconnected");
            }
        }
    }
    public async void Disconnect()
    {
        if (m_onDisconnecting)
        { return; }
        m_onDisconnecting = true;

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            if (m_debugMode)
            {
                Debug.Log("Already disconnected");
            }
            return;
        }
        if (NetworkManager.Singleton.IsHost)
        {
            await ShutdownHostInternal();
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            await DisconnectClientInternal();
        }
        m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
        CleanupAfterDisconnect();
    }
    private async Task ShutdownHostInternal()
    {
        if (m_debugMode)
        {
            Debug.Log("Shuttingdown host");
        }

        try
        {
            if (m_currentLobby != null)
            {
                await LobbyService.Instance.DeleteLobbyAsync(m_currentLobby.Id);
            }
            else
            {
                if (m_debugMode)
                {
                    Debug.Log("Lobby does not exist");
                }
            }

            m_currentLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        GameMaster.Instance.ClearupAfterDisconnect();

        NetworkManager.Singleton.Shutdown();
    }
    private async Task DisconnectClientInternal()
    {
        if (m_debugMode)
        {
            Debug.Log("Dissconecting client");
        }

        try
        {
            if (m_currentLobby != null)
            {
                await LobbyService.Instance.RemovePlayerAsync(m_currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            else
            {
                if (m_debugMode)
                {
                    Debug.Log("Lobby dont exist");
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        GameMaster.Instance.ClearupAfterDisconnect();
        m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
        NetworkManager.Singleton.Shutdown();
    }
    async void HearthbeatLobby()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            if (m_currentLobby != null && m_currentMultiplayerStatus == MultiplayerStatus.InLobbyStatus)
            {

                await LobbyService.Instance.SendHeartbeatPingAsync(m_currentLobby.Id);
                if (m_debugMode)
                {
                    Debug.Log("Lobby Heartbeat sent");
                }

            }
        }
    }
    async Task InitUnityServices()
    {
        await UnityServices.InitializeAsync();

        string _profileName = UnityEngine.Random.Range(100000, 999999).ToString();


        AuthenticationService.Instance.SwitchProfile(_profileName);

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        if (m_debugMode) Debug.Log("PlayerID: " +
            AuthenticationService.Instance.PlayerId);
    }

    public async Task StartHost()
    {
        if (m_isRelayAction)
            return;

        m_isRelayAction = true;

        try
        {
            await m_bootsTrap.RecreateNetworkManager();

            string _joinCode =
                await CreateRelay();

            if (!NetworkManager.Singleton.IsHost)
            {
                Debug.LogError(
                    "MultiplayerManager: host was not started.");

                return;
            }

            await m_bootsTrap.RecreateGameMaster();

            m_gameMaster =
                m_bootsTrap.m_gameMasterInstance;

            if (m_gameMaster == null)
            {
                Debug.LogError(
                    "MultiplayerManager: GameMaster was not created.");

                return;
            }

            var _options =
                new CreateLobbyOptions
                {
                    Data =
                        new Dictionary<string, DataObject>
                        {
                        {
                            "joinCode",
                            new DataObject(
                                DataObject.VisibilityOptions.Public,
                                _joinCode)
                        }
                        }
                };

            m_currentLobby =
                await LobbyService.Instance.CreateLobbyAsync(
                    "MyLobby",
                    4,
                    _options);

            SetLobbyCode();

            m_currentMultiplayerStatus =
                MultiplayerStatus.InLobbyStatus;

            if (m_debugMode)
            {
                Debug.Log(
                    "Lobby created with code: " +
                    _joinCode);
            }
        }
        finally
        {
            m_isRelayAction = false;
        }
    }

    async Task<string> CreateRelay()
    {
        Allocation _allocation =
            await RelayService.Instance.CreateAllocationAsync(4);

        string _joinCode =
            await RelayService.Instance.GetJoinCodeAsync(
                _allocation.AllocationId);

        UnityTransport _transport =
            NetworkManager.Singleton
                .GetComponent<UnityTransport>();

        _transport.SetRelayServerData(
            _allocation.RelayServer.IpV4,
            (ushort)_allocation.RelayServer.Port,
            _allocation.AllocationIdBytes,
            _allocation.Key,
            _allocation.ConnectionData);

        bool started =
            NetworkManager.Singleton.StartHost();

        if (!started)
        {
            throw new System.InvalidOperationException(
                "Failed to start host.");
        }

        return _joinCode;
    }

    public async Task FindAndJoinLobby()
    {
        if (m_isRelayAction)
            return;

        m_isRelayAction = true;

        try
        {
            await m_bootsTrap.RecreateNetworkManager();

            var _lobbies =
                await LobbyService.Instance.QueryLobbiesAsync();

            if (_lobbies.Results.Count == 0)
            {
                if (m_debugMode)
                {
                    Debug.Log(
                        "Lobby not found!");
                }

                return;
            }

            Lobby _lobby =
                _lobbies.Results[0];

            m_currentLobby =
                await LobbyService.Instance.JoinLobbyByIdAsync(
                    _lobby.Id);

            string _joinCode =
                m_currentLobby.Data["joinCode"].Value;

            await JoinRelay(_joinCode);

            await WaitForGameMaster();

            m_gameMaster =
                GameMaster.Instance.gameObject;

            SetLobbyCode();

            m_currentMultiplayerStatus =
                MultiplayerStatus.InLobbyStatus;
        }
        finally
        {
            m_isRelayAction = false;
        }
    }

    public async Task JoinLobbyByJoinCode()
    {
        if (m_isRelayAction)
            return;

        if (string.IsNullOrEmpty(m_joinCodeInput))
        {
            Debug.LogWarning(
                "Join code is empty!");

            return;
        }

        m_isRelayAction = true;

        try
        {
            await m_bootsTrap.RecreateNetworkManager();

            m_currentLobby =
                await LobbyService.Instance.JoinLobbyByCodeAsync(
                    m_joinCodeInput);

            string _joinCode =
                m_currentLobby.Data["joinCode"].Value;

            await JoinRelay(_joinCode);

            await WaitForGameMaster();

            m_gameMaster =
                GameMaster.Instance.gameObject;

            SetLobbyCode();

            m_currentMultiplayerStatus =
                MultiplayerStatus.InLobbyStatus;
        }
        finally
        {
            m_isRelayAction = false;
        }
    }
    private async Task WaitForGameMaster()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (GameMaster.Instance == null)
        {
            if (elapsed >= timeout)
            {
                Debug.LogError(
                    "MultiplayerManager: GameMaster synchronization timeout.");

                return;
            }

            elapsed += Time.deltaTime;

            await Task.Yield();
        }
    }

    async Task JoinRelay(string joinCode)
    {
        var _joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        _transport.SetRelayServerData(

            _joinAllocation.RelayServer.IpV4,
            (ushort)_joinAllocation.RelayServer.Port,
            _joinAllocation.AllocationIdBytes,
            _joinAllocation.Key,
            _joinAllocation.ConnectionData,
            _joinAllocation.HostConnectionData);
        NetworkManager.Singleton.StartClient();

        if (m_debugMode)
        {
            Debug.Log("Joined loby with code: " + joinCode);
        }
    }

    public async Task DisconnectClient()
    {
        if (m_currentLobby != null)
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(m_currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch
            {
                Debug.LogError("Failed to leave lobby");
            }

            m_currentLobby = null;
        }

        NetworkManager.Singleton.Shutdown();

        if (m_debugMode)
        {
            Debug.Log("Client disconnected");
        }
    }

    public bool LobbyConnected()
    {
        return m_currentLobby != null;
    }
    #endregion

    private void CheckRef()
    {
        if (m_disconnectMenuUI == null)
        {
            DisconnectMenuUI[] _searchResult = FindObjectsByType<DisconnectMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (_searchResult.Length > 0)
            {
                if (_searchResult.Length > 1)
                {
                    Debug.LogError("DisconnectMenu Duplicate!");
                }
                else if (_searchResult.Length == 1)
                {
                    m_disconnectMenuUI = _searchResult[0];
                }
            }
        }
    }


    //todo Need rework?
    private void CleanupAfterDisconnect()
    {
        if(m_debugMode)
        {
            Debug.Log("Cleanup calling");
        }

        m_onDisconnecting = false;
        m_onApplicationClosing = false;

        SessionManager.Instance.ClearSessionOnDisconnect();
        GameMaster.Instance.ClearupAfterDisconnect();
        m_currentLobby = null;
        m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
        LobbyControlUI.Instance.gameObject.SetActive(true);
        //ToDo
        //m_lobbyCode.text = string.Empty;
        //m_lobbyCodePanel.SetActive(false);
    }
    //todo need rework
    void SetLobbyCode()
    {
        Debug.Log("SetLobbyCode");
        Debug.Log("Lobby code: " + m_currentLobby.LobbyCode);
        //m_lobbyCodePanel.SetActive(true);
        //m_lobbyCode.text = m_currentLobby.LobbyCode;
    }
}