
using System.Collections;
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
using UnityEngine.SceneManagement;
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
    private bool m_isLeavingToResult = false;
    [SerializeField] private float m_resultLeaveTimeout = 3f;
    //ToDo Need full rework
    public TMP_Text m_lobbyCode;
    public GameObject m_lobbyCodePanel;

    private const int m_maxLobbyPlayers = 2;
    private const int m_relayClientCount = 1;

    private List<Lobby> m_knowLobbies = new List<Lobby>();

    private Coroutine m_startGameSceneCoroutine;
    private bool m_isStartingGameScene = false;

    private const float m_clientConnectTimeout = 10f;
    private const float m_startGameConnectTimeout = 10f;

    public IReadOnlyList<Lobby> KnowLobbies
    {
        get
        {
            return m_knowLobbies;
        }
    }

    private DisconnectMenuUI m_disconnectMenuUI;

    public static MultiplayerManager Instance;

    public MultiplayerStatus m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;

    [Header ("Manager Object")]
    [SerializeField] private GameObject m_gameMaster;

    [SerializeField] private GameObject m_sessionManager;

    [SerializeField] private Bootstrap m_bootsTrap;
    public bool m_isRelayAction = false;

    private bool m_isSubscribedToDisconnectEvents = false;

    private bool m_isSubscribedToSceneEvent = false;
    #endregion

    #region UnityCallingFunc
    async void Start()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        if(AuthManager.Instance != null)
        {
            await AuthManager.Instance.Initialize();
        }
        else
        {
            Debug.LogError("MultiplayerManager: No auth manager");
        }

        await m_bootsTrap.RecreateNetworkManager();

        SubscribeNetworkEvents();

        InvokeRepeating(nameof(HearthbeatLobby), 15f, 15f);
    }
    private void OnDestroy()
    {
        UnsubscribeNetworkEvents();

        if(Instance == this)
        {
            Instance = null;
        }

        m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
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
    private void SubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton == null)
            return;
        if (!m_isSubscribedToDisconnectEvents)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            m_isSubscribedToDisconnectEvents = true;
        }

        SubscribeSceneEventsIfReady();
    }

    private void SubscribeSceneEventsIfReady()
    {
        if (NetworkManager.Singleton == null)
            return;
        if (m_isSubscribedToSceneEvent)
            return;

        if(NetworkManager.Singleton.SceneManager == null)
        {
            if(m_debugMode)
            {
                Debug.Log("NetworkSceneManager not ready yet");
            }
            return;
        }

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

        m_isSubscribedToSceneEvent = true;
    }
    private void UnsubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton == null)
        {
            m_isSubscribedToDisconnectEvents = false;
           m_isSubscribedToSceneEvent = false;
            return;
        }
        if(m_isSubscribedToDisconnectEvents)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        if(m_isSubscribedToSceneEvent&&NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;

        m_isSubscribedToDisconnectEvents = false;
        m_isSubscribedToSceneEvent=false;
    }
    private void OnClientDisconnected(ulong clientId)
    {
        if (m_isLeavingToResult)
        {
            if (m_debugMode) Debug.Log("Disconnect ignored while leaving for ResultScene.");
            return;
        }

        if (m_debugMode)
        {
            Debug.Log("Client disconnected: " + clientId);
        }

        if (NetworkManager.Singleton == null)
        {
            HandleLostConnectionToHost();
            return;
        }

        bool _isServer = NetworkManager.Singleton.IsServer;

        bool _isLocalClientDisconnected = clientId == NetworkManager.Singleton.LocalClientId;

        bool _isHostDisconnectedForClient = !_isServer && clientId == NetworkManager.ServerClientId;

        if(_isServer && !_isLocalClientDisconnected)
        {
            Debug.LogWarning("Remote client disconnected. Host continue");
        }

        if (_isLocalClientDisconnected || _isHostDisconnectedForClient)
        {
            if (m_debugMode)
            {
                Debug.Log(
                    "Local client or host/server disconnected. " +
                    "Returning to lobby search UI.");
            }

            HandleLostConnectionToHost();
        }
    }
    private async void HandleLostConnectionToHost()
    {
        if (m_onDisconnecting)
            return;

        m_onDisconnecting = true;

        try
        {
            if (m_currentLobby != null)
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(
                        m_currentLobby.Id,
                        AuthenticationService.Instance.PlayerId);
                }
                catch (LobbyServiceException e)
                {
                    if (m_debugMode)
                    {
                        Debug.Log(
                            "RemovePlayerAsync failed after host disconnect. " +
                            "This is acceptable if lobby was deleted by host. " +
                            e.Message);
                    }
                }
            }
            if(GameMaster.Instance != null)
            {
                GameMaster.Instance.ClearupAfterDisconnect();
            }

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        finally
        {
            CleanupAfterDisconnect();
        }
    }
    public async void Disconnect()
    {
        if (m_onDisconnecting)
            return;

        m_onDisconnecting = true;

        try
        {
            if (NetworkManager.Singleton == null)
            {
                CleanupAfterDisconnect();
                return;
            }

            if (!NetworkManager.Singleton.IsClient &&
                !NetworkManager.Singleton.IsHost)
            {
                if (m_debugMode)
                {
                    Debug.Log("Already disconnected");
                }

                CleanupAfterDisconnect();
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

            m_currentMultiplayerStatus =
                MultiplayerStatus.InMenuStatus;

            CleanupAfterDisconnect();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            CleanupAfterDisconnect();
        }
    }
    public async void LeaveMatchToResultScene()
    {
        if (m_isLeavingToResult)
            return;

        m_isLeavingToResult = true;
        m_onDisconnecting = true;

        NetworkManager networkManager = NetworkManager.Singleton;
        bool isHost = networkManager != null && networkManager.IsHost;

        try
        {
            if (isHost)
            {
                float deadline = Time.realtimeSinceStartup + m_resultLeaveTimeout;
                while (networkManager != null && networkManager.IsListening &&
                       networkManager.ConnectedClientsIds.Count > 1 && Time.realtimeSinceStartup < deadline)
                {
                    await Task.Delay(100);
                }
            }

            try
            {
                if (m_currentLobby != null)
                {
                    if (isHost)
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(m_currentLobby.Id);
                    }
                    else
                    {
                        await LobbyService.Instance.RemovePlayerAsync(
                            m_currentLobby.Id, AuthenticationService.Instance.PlayerId);
                    }
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning(e.Message);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            try
            {
                try
                {
                    if (GameMaster.Instance != null)
                    {
                        GameMaster.Instance.ClearupAfterDisconnect();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }

                if (networkManager != null && networkManager.IsListening)
                {
                    networkManager.Shutdown();
                }

                // Shutdown is deferred; suppress disconnect redirects until its callbacks finish.
                while (networkManager != null && networkManager.ShutdownInProgress)
                {
                    await Task.Yield();
                }

                try
                {
                    if (SessionManager.Instance != null)
                    {
                        SessionManager.Instance.ClearSessionOnDisconnect();
                    }
                }
                finally
                {
                    m_currentLobby = null;
                    m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
                    m_isStartingGameScene = false;

                    if (m_startGameSceneCoroutine != null)
                    {
                        StopCoroutine(m_startGameSceneCoroutine);
                        m_startGameSceneCoroutine = null;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                try
                {
                    if (SceneFlowManager.Instance != null)
                    {
                        SceneFlowManager.Instance.LoadResultScene();
                    }
                }
                finally
                {
                    m_onDisconnecting = false;
                    m_isLeavingToResult = false;
                }
            }
        }
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
        if(GameMaster.Instance != null)
            GameMaster.Instance.ClearupAfterDisconnect();
        if(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
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
        if(GameMaster.Instance != null)
            GameMaster.Instance.ClearupAfterDisconnect();
        m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
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

    //todo Dont use rn
    /*
    async Task InitUnityServices()
    {
        await UnityServices.InitializeAsync();

        string _profileName = UnityEngine.Random.Range(100000, 999999).ToString();


        AuthenticationService.Instance.SwitchProfile(_profileName);

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        if (m_debugMode) Debug.Log("PlayerID: " +
            AuthenticationService.Instance.PlayerId);
    }
    */
    public async Task StartHost()
    {
        if (m_isRelayAction)
            return;

        m_isRelayAction = true;

        try
        {
            UnsubscribeNetworkEvents();

            await m_bootsTrap.RecreateNetworkManager();

            SubscribeNetworkEvents();

            string _joinCode = await CreateRelay();

            SubscribeSceneEventsIfReady();

            if (!NetworkManager.Singleton.IsHost)
            {
                Debug.LogError("MultiplayerManager: host was not started.");
                return;
            }

            var _options =
                new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Player = CreateLobbyPlayerData("Host"),
                    Data =
                        new Dictionary<string, DataObject>
                        {
                            {
                            "joinCode",
                            new DataObject(
                                DataObject.VisibilityOptions.Public,
                                _joinCode)
                            },
                            {
                                "gameMode",
                                new DataObject(DataObject.VisibilityOptions.Public, "TwoPlayers")
                            }
                        }
                };

            m_currentLobby =
                await LobbyService.Instance.CreateLobbyAsync(
                    "MyLobby",
                    m_maxLobbyPlayers,
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
        catch(LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        finally
        {
            m_isRelayAction = false;
        }
    }

    async Task<string> CreateRelay()
    {
        Allocation _allocation =
            await RelayService.Instance.CreateAllocationAsync(m_relayClientCount);

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

    public async Task<List<Lobby>> FindAvailableLobbies()
    {
        m_knowLobbies.Clear();

        try
        {
            QueryLobbiesOptions _options =
                new QueryLobbiesOptions
                {
                    Count = 25,

                    Filters =
                        new List<QueryFilter>
                        {
                        new QueryFilter(
                            QueryFilter.FieldOptions.AvailableSlots,
                            "0",
                            QueryFilter.OpOptions.GT)
                        },

                    Order =
                        new List<QueryOrder>
                        {
                        new QueryOrder(
                            false,
                            QueryOrder.FieldOptions.Created)
                        }
                };

            QueryResponse _response =
                await LobbyService.Instance.QueryLobbiesAsync(
                    _options);

            Debug.Log(
                "FindAvailableLobbies: query result count = " +
                _response.Results.Count);

            foreach (Lobby _lobby in _response.Results)
            {
                if (_lobby == null)
                    continue;

                Debug.Log(
                    "Found lobby before filters: " +
                    _lobby.Name +
                    " Players: " +
                    _lobby.Players.Count +
                    "/" +
                    _lobby.MaxPlayers +
                    " Id: " +
                    _lobby.Id);

                if (_lobby.MaxPlayers != m_maxLobbyPlayers)
                {
                    Debug.Log(
                        "Skip lobby. Wrong MaxPlayers: " +
                        _lobby.MaxPlayers);

                    continue;
                }

                if (_lobby.Players.Count >= m_maxLobbyPlayers)
                {
                    Debug.Log(
                        "Skip lobby. Lobby is full.");

                    continue;
                }

                if (_lobby.Data == null)
                {
                    Debug.Log(
                        "Skip lobby. Data is null.");

                    continue;
                }

                if (!_lobby.Data.ContainsKey("joinCode"))
                {
                    Debug.Log(
                        "Skip lobby. No joinCode.");

                    continue;
                }

                if (_lobby.Data.ContainsKey("gameMode") &&
                    _lobby.Data["gameMode"].Value != "TwoPlayers")
                {
                    Debug.Log(
                        "Skip lobby. Wrong gameMode: " +
                        _lobby.Data["gameMode"].Value);

                    continue;
                }

                m_knowLobbies.Add(_lobby);

                Debug.Log(
                    "Lobby added to known list: " +
                    _lobby.Name);
            }

            Debug.Log(
                "FindAvailableLobbies: final known count = " +
                m_knowLobbies.Count);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }

        return m_knowLobbies;
    }

    public async Task JoinLobbyById(string lobbyId)
    {
        if (m_isRelayAction)
            return;

        if(string.IsNullOrEmpty(lobbyId))
        {
            Debug.LogWarning("JoinLobbyById: LobbyId is null");
            return;
        }

        m_isRelayAction = true;

        try
        {
            UnsubscribeNetworkEvents();

            await m_bootsTrap.RecreateNetworkManager();

            SubscribeNetworkEvents();

            JoinLobbyByIdOptions _options = new JoinLobbyByIdOptions
            {
                Player = CreateLobbyPlayerData("Client")
            };

            m_currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, _options);

            if(m_currentLobby.Data == null||!m_currentLobby.Data.ContainsKey("joinCode"))
            {
                Debug.LogError("SelectedLobby does not have joinCode");
                return;
            }

            string _joinCode = m_currentLobby.Data["joinCode"].Value;


            bool _relayJoined = await JoinRelay(_joinCode);
            if(!_relayJoined)
            {
                Debug.LogError("Failed to join Relay");
                await LeaveCurrentLobbyAfterFailedJoin();
                return;
            }

            SetLobbyCode();

            m_currentMultiplayerStatus = MultiplayerStatus.InLobbyStatus;

            if(m_debugMode)
            {
                Debug.Log("Joinde selected lobby: " + m_currentLobby.Name);
            }
        }
        catch(LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        finally
        {
            m_isRelayAction = false;
        }
    }

    public async Task RefreshCurrentLobby()
    {
        if (m_currentLobby == null)
            return;

        try
        {
            m_currentLobby = await LobbyService.Instance.GetLobbyAsync(m_currentLobby.Id);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
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

            bool _relayJoined = await JoinRelay(_joinCode);
            if (!_relayJoined)
            {
                Debug.LogError("Failed to join Relay");
                await LeaveCurrentLobbyAfterFailedJoin();
                return;
            }

            SetLobbyCode();

            m_currentMultiplayerStatus =
                MultiplayerStatus.InLobbyStatus;
        }
        finally
        {
            m_isRelayAction = false;
        }
    }

    async Task<bool> JoinRelay(string joinCode)
    {
        try
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
            bool _isStarted = NetworkManager.Singleton.StartClient();

            if (!_isStarted)
            {
                Debug.LogError("Failed to start client");
                return false;
            }

            SubscribeSceneEventsIfReady();

            bool _connected = await WaitForLocalClientConnected(m_clientConnectTimeout);
            if (!_connected)
            {
                Debug.LogError("JoinRelay : client connection timed out");
                if(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                return false;
            }
            if (m_debugMode)
            {
                Debug.Log("Joined loby with code: " + joinCode);
            }
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            return false;
        }
    }

    private async Task<bool> WaitForLocalClientConnected(float timeoutSeconds)
    {
        if(NetworkManager.Singleton == null)
            return false;

        if (NetworkManager.Singleton.IsConnectedClient)
            return true;

        TaskCompletionSource<bool> _connectionResult = new TaskCompletionSource<bool> ();

        void OnConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return;

            if(NetworkManager.Singleton.IsConnectedClient || clientId == NetworkManager.Singleton.LocalClientId)
            {
                _connectionResult.TrySetResult(true);
            }
        }

        void OnDisconnected(ulong clientId)
        {
            if(NetworkManager.Singleton == null)
            {
                _connectionResult.TrySetResult(false);
                return;
            }

            if(clientId==NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
            {
                _connectionResult.TrySetResult(false);
            }
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

        try
        {
            Task _timeOutTask = Task.Delay((int)(timeoutSeconds * 1000f));

            Task _completeTask = await Task.WhenAny(_connectionResult.Task, _timeOutTask);

            if (_completeTask != _connectionResult.Task)
                return false;

            return await _connectionResult.Task;
        }
        finally
        {
            if(NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
            }
        }
    }

    private async Task LeaveCurrentLobbyAfterFailedJoin()
    {
        if (m_currentLobby == null)
            return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(m_currentLobby.Id, AuthenticationService.Instance.PlayerId);
        }
        catch (LobbyServiceException e)
        {
            if(m_debugMode)
            {
                Debug.LogWarning("Leave current lobby after failed join" + e.Message);
            }
        }

        m_currentLobby = null;
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

    public void StartGameScene()
    {
        if(NetworkManager.Singleton == null)
        {
            Debug.LogError("StartGameScene: NetworkSingleton is null");
            return;
        }

        if(!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Start game scene rejected. Is not a server/host");
            return;
        }


        if (!CanStartTwoPlayerGame())
        {
            Debug.LogWarning("Cannot start game. Need exatly 2 players");
            return;
        }



        if (m_currentLobby == null)
        {
            Debug.LogError(" Start game scene rejected: No lobby");
            return;
        }

        if(SceneFlowManager.Instance == null)
        {
            Debug.LogError("Scene flow manager dont exist");
            return;
        }

        if ((m_isStartingGameScene))
        {
            Debug.LogWarning("Start game scene already in progress");
            return;
        }

        m_startGameSceneCoroutine = StartCoroutine(StartGameSceneWhenReady());
    }

    private IEnumerator StartGameSceneWhenReady()
    {
        m_isStartingGameScene = true;

        float _timer = 0f;
        while(!IsNetworkReadyForTwoPlayerGame())
        {
            _timer += Time.deltaTime;

            if(_timer >= m_startGameConnectTimeout)
            {
                Debug.LogWarning("Cannot start game. Need exatly 2 connected NGO clients.\nConnected: " + GetConnectedClientCount());
                m_isStartingGameScene = false;
                m_startGameSceneCoroutine = null;

                yield break;
            }
            yield return null;
        }

        yield return RefreshCurrentLobbyCoroutine();

        if(!CanStartTwoPlayerGame())
        {
            Debug.LogWarning("Cannot start game. Need exatly 2 lobby players.");
            m_isStartingGameScene = false;
            m_startGameSceneCoroutine = null;

            yield break;
        }

        SceneFlowManager.Instance.LoadGameSceneNetwork();

        m_startGameSceneCoroutine = null;
    }

    private IEnumerator RefreshCurrentLobbyCoroutine()
    {
        Task _refreshTask = RefreshCurrentLobby();

        while(!_refreshTask.IsCompleted)
        {
            yield return null;
        }

        if(_refreshTask.IsFaulted)
        {
            Debug.LogError(_refreshTask.Exception);
        }
    }

    private bool IsNetworkReadyForTwoPlayerGame()
    {
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            NetworkManager.Singleton.IsListening &&
            GetConnectedClientCount() == m_maxLobbyPlayers;
    }

    private int GetConnectedClientCount()
    {
        if(NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClients == null)
        {
            return 0;
        }

        return NetworkManager.Singleton.ConnectedClients.Count;
    }

    private async void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientTimeOut)
    {
        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.MarkNetworkSceneLoadingFinished();
            m_isStartingGameScene = false;
        }
        else
        {
            return;
        }

        if (sceneName != SceneFlowManager.Instance.GameSceneName)
            return;

        m_currentMultiplayerStatus =
            MultiplayerStatus.InGameStatus;

        if (!NetworkManager.Singleton.IsServer)
            return;

        await m_bootsTrap.RecreateGameMaster();

        m_gameMaster =
            m_bootsTrap.m_gameMasterInstance;

        if (m_gameMaster == null)
        {
            Debug.LogError(
                "OnLoadEventCompleted: Cant create game master");
            return;
        }

        if (GameMaster.Instance == null)
        {
            Debug.LogError(
                "Game master not linked to Instance");
            return;
        }

        StartCoroutine(
            StartGameAfterSceneReady());
    }
    private IEnumerator StartGameAfterSceneReady()
    {
        float _timeout = 10f;
        float _timer = 0f;

        while (SessionManager.Instance == null)
        {
            _timer += Time.deltaTime;

            if (_timer >= _timeout)
            {
                Debug.LogError(
                    "StartGameAfterSceneReady: SessionManager.Instance is null.");

                yield break;
            }

            yield return null;
        }

        _timer = 0f;

        while (SessionManager.Instance.m_playerSessions.Count < m_maxLobbyPlayers)
        {
            _timer += Time.deltaTime;

            if (_timer >= _timeout)
            {
                Debug.LogError(
                    "StartGameAfterSceneReady: not enough PlayerSessions. " +
                    "Current: " +
                    SessionManager.Instance.m_playerSessions.Count);

                yield break;
            }

            yield return null;
        }

        GameMaster.Instance.SpawnPlayers();

        GameMaster.Instance.StartNewGame();
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

    public Lobby GetCurrentLobby()
    {
        return m_currentLobby;
    }

    private Player CreateLobbyPlayerData(string playerRole)
    {
        string _playerId = AuthenticationService.Instance.PlayerId;

        string _shortId = _playerId;

        if(!string.IsNullOrEmpty(_playerId)&&_playerId.Length>6)
        {
            _shortId = _playerId.Substring(0, 6);
        }

        string _playerName = playerRole + "_" + _shortId;

        return new Player
            (id: _playerId,
            data: new Dictionary<string, PlayerDataObject>
            {
                {
                    "playerName", new PlayerDataObject(visibility: PlayerDataObject.VisibilityOptions.Public, value: _playerName)
                },
                {
                    "role", new PlayerDataObject(visibility: PlayerDataObject.VisibilityOptions.Public, value: playerRole)
                }
            });
    }

    private void CleanupAfterDisconnect()
    {
        if(m_debugMode)
        {
            Debug.Log("Cleanup calling");
        }

        m_onDisconnecting = false;
        m_onApplicationClosing = false;

        if(SessionManager.Instance != null)
        {
            SessionManager.Instance.ClearSessionOnDisconnect();
        }
        
        m_currentLobby = null;
        m_currentMultiplayerStatus = MultiplayerStatus.InMenuStatus;

        if(GameMaster.Instance != null )
        {
            GameMaster.Instance.ClearupAfterDisconnect();
        }
        
        if(SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadLobbyScene();
        }

        m_isStartingGameScene = false;

        if(m_startGameSceneCoroutine != null)
        {
            StopCoroutine(m_startGameSceneCoroutine);
            m_startGameSceneCoroutine = null;
        }
    }
    void SetLobbyCode()
    {
        Debug.Log("SetLobbyCode");
        Debug.Log("Lobby code: " + m_currentLobby.LobbyCode);
    }

    public bool IsCurrentLobbyFull()
    {
        return m_currentLobby != null && m_currentLobby.Players != null &&
            m_currentLobby.Players.Count >= m_maxLobbyPlayers;
    }

    public bool CanStartTwoPlayerGame()
    {
        if (m_currentLobby == null)
            return false;

        if(m_currentLobby.Players == null)
            return false;

        return m_currentLobby.Players.Count == m_maxLobbyPlayers;
    }
}
