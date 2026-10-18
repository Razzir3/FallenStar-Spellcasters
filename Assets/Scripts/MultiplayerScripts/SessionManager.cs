using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System;

public struct SessionData : INetworkSerializable, IEquatable<SessionData>
{
    public ulong ClientId;
    public NetworkObjectReference SessionRef;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref SessionRef);
    }

    public bool Equals(SessionData other)
    {
        return ClientId == other.ClientId;
    }
}

public class SessionManager : NetworkBehaviour
{
    #region Variable
    public static SessionManager Instance;
    public bool m_debugMode = false;

    public PlayerSession LocalPlayer;
    public Dictionary<ulong, PlayerSession> m_playerSessions = new();
    public NetworkList<SessionData> SessionDataList;

    private GameSlot[] m_gameSlots;

    [Header("Session Prefabs")]

    [SerializeField] private GameObject m_playerSessionPrefab;
    #endregion

    #region UnityCallingMethods
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_gameSlots = new GameSlot[4];
        for(int i = 0;i<m_gameSlots.Length;i++)
        {
            m_gameSlots[i] = new GameSlot();
        }
        SessionDataList = new NetworkList<SessionData>();
        Instance = this;
    }
    #endregion
    private int GetFreeSlot(ulong clientId)
    {
        for(int i = 0; i<m_gameSlots.Length;i++)
        {
            if(m_gameSlots[i].m_isOccupied==false)
            {
                m_gameSlots[i].m_isOccupied = true;
                m_gameSlots[i].m_playerId = clientId;
                return i;
            }
        }
        return -1;
    }
    #region OnCallingUpdates
    public override void OnNetworkSpawn()
    {
        SessionDataList.OnListChanged += OnSessionListChanged;

        RebuildDictionary();

        if (!IsServer)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback +=
            OnClientConnected;

        NetworkManager.Singleton.OnClientDisconnectCallback +=
            OnClientDisconnected;

        CreateSessionsForAlreadyConnectedClients();
    }

    private void CreateSessionsForAlreadyConnectedClients()
    {
        if (!IsServer)
            return;

        if (NetworkManager.Singleton == null)
            return;

        foreach (ulong clientId in
                 NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (m_playerSessions.ContainsKey(clientId))
            {
                continue;
            }

            CreateSession(clientId);
        }
    }

    private void OnSessionListChanged(NetworkListEvent<SessionData> changeEvent)
    {
        RebuildDictionary();
    }
    private void RebuildDictionary()
    {
        m_playerSessions.Clear();

        foreach (var _sessionData in SessionDataList)
        {
            if (_sessionData.SessionRef.TryGet(out NetworkObject obj))
            {
                PlayerSession _session = obj.GetComponent<PlayerSession>();

                m_playerSessions[_sessionData.ClientId] = _session;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!NetworkManager.Singleton)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        SessionDataList.OnListChanged -= OnSessionListChanged;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (m_debugMode)
        {
            Debug.Log("Client connected: " + clientId);
        }
        CreateSession(clientId);
    }
    #endregion


    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && GameMaster.Instance != null &&
            m_playerSessions.TryGetValue(clientId, out PlayerSession _leaving) && _leaving != null)
        {
            GameMaster.Instance.HandlePlayerLeft(_leaving);
        }

        if (m_debugMode)
        {
            Debug.Log("Client disconnected: " + clientId);
        }

        for (int i = 0; i < m_gameSlots.Length; i++)
        {
            if (m_gameSlots[i].m_playerId == clientId)
            {
                m_gameSlots[i].m_isOccupied = false;
            }
        }
        for (int i = SessionDataList.Count - 1; i >= 0; i--)
        {
            if (SessionDataList[i].ClientId == clientId)
            {
                SessionDataList.RemoveAt(i);
            }
        }
        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.ClearLocalUI();
        }

        if (m_playerSessions.TryGetValue(clientId, out PlayerSession session))
        {
            if (session != null &&
                session.NetworkObject != null &&
                session.NetworkObject.IsSpawned)
            {
                session.NetworkObject.Despawn(true);
            }

            m_playerSessions.Remove(clientId);
        }
    }

    private void CreateSession(ulong clientId)
    {
        if(m_playerSessions.ContainsKey(clientId))
        {
            if (m_debugMode)
            {
                Debug.Log("CS Session Manager");
            }
            Debug.LogError("Duplicate session key");
            return;
        }

        GameObject _object = Instantiate(m_playerSessionPrefab);

        if(_object == null)
        {
            Debug.LogError("No session prefab");
            return;
        }

        NetworkObject _networkObject = _object.GetComponent<NetworkObject>();

        if(_networkObject != null)
        {
            PlayerSession _session = _object.GetComponent<PlayerSession>();
            
            if(_session == null)
            {
                Debug.LogError("No player session component on session prefab");
            }

            int _teamAssing = GetFreeSlot(clientId);

            
            _session.Initialize(clientId, _teamAssing);

            _networkObject.SpawnWithOwnership(clientId);

            m_playerSessions.Add(clientId, _session);

            SessionDataList.Add(new SessionData
            {
                ClientId = clientId,
                SessionRef = _session.NetworkObject
            });

            if(m_debugMode)
            {
                Debug.Log("Session created for client: " + clientId);
                Debug.Log("TeamId" + _session.TeamId.Value);
            }
        }
        else
        {
            Debug.LogError("Player session prefab does not have a NetworkObject component.");
        }
    }

    public PlayerSession GetPlayerSession(ulong clientId)
    {
        if(m_playerSessions.ContainsKey(clientId))
        {
            return m_playerSessions[clientId];
        }
        return null;
    }

    public void ClearSessionOnDisconnect()
    {
        if(IsServer)
        foreach (var _session in m_playerSessions.Values)
        {
            if (_session != null && _session.NetworkObject.IsSpawned)
            {
                var _character = _session.GetCharacter();
                    if (_character != null && _character.NetworkObject != null && _character.NetworkObject.IsSpawned)
                    {
                        _character.NetworkObject.Despawn(true);
                    }

                _session.NetworkObject.Despawn(true);
            }
        }
        m_playerSessions.Clear();
      
        LocalPlayer = null;
        for(int i = 0; i<m_gameSlots.Length;i++)
        {
            m_gameSlots[i].m_isOccupied = false;
        }
    }
}
