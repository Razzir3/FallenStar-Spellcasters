using Unity.Netcode;
using UnityEngine;

public partial class GameMaster
{
    #region Player
    private void SpawnCharacter(PlayerSession session)
    {
        if (m_debugMode)
        {
            Debug.Log("Attempting to spawn character for client: " + session.ClientId.Value);
        }

        if (!IsServer)
        {
            return;
        }

        if (!SessionManager.Instance.m_playerSessions.ContainsKey(session.ClientId.Value))
        {
            Debug.LogError(session.ClientId.Value + " does not have a session.");
            return;
        }

        if (session.GetCharacter() != null)
        {
            Debug.LogError(session.ClientId.Value + " already has a character.");
            return;
        }

        SpawnPoint _spawnPoint = GetSpawnPoint(session.TeamId.Value);

        if (_spawnPoint == null)
        {
            Debug.LogError("No spawn point available for team: " + session.TeamId.Value);
            return;
        }

        GameObject _characterObject = Instantiate(m_playerCharacterPrefab, _spawnPoint.transform.position, _spawnPoint.transform.rotation);

        NetworkObject _networkObject = _characterObject.GetComponent<NetworkObject>();

        if (_networkObject != null)
        {
            PlayerController _playerController = _characterObject.GetComponent<PlayerController>();

            if (_playerController == null)
            {
                Debug.LogError("Player prefab dont have any player controller");
                return;
            }

            _networkObject.SpawnAsPlayerObject(session.ClientId.Value);

            _playerController.SessionObjectId.Value = session.NetworkObjectId;

            _playerController.Initialize(session);

            session.CharacterObjectId.Value = _networkObject.NetworkObjectId;

            session.SetSpawnPoint(_spawnPoint);

            session.SetCharacter(_playerController);

            session.MovePlayeToSpawnPoint();

            _spawnPoint.IsOccupied = true;

            if ((m_debugMode))
            {
                Debug.Log("Character spawned for client: " + session.ClientId.Value + " at spawn point: " + _spawnPoint.name);
            }
        }
        else
        {
            Debug.LogError("Player character prefab does not have a NetworkObject component.");
        }
    }

    private void RespawnCharacter(PlayerSession session)
    {
        if (m_debugMode)
        {
            Debug.Log("Attempting to respawn character for client: " + session.ClientId.Value);
        }

        if (!IsServer)
            return;

        if (session == null)
            return;

        if (!SessionManager.Instance.m_playerSessions.ContainsKey(session.ClientId.Value))
        {
            Debug.LogError(session.ClientId.Value + " does not have a session.");

            return;
        }

        if (session.m_lifeCurrent.Value <= 0)
        {
            return;
        }

        if (session.GetCharacter() != null)
        {
            Debug.LogError(session.ClientId.Value + " already has a character.");

            return;
        }

        SpawnPoint _spawnPoint = session.GetSpawnPoint();

        if (_spawnPoint == null)
        {
            Debug.LogWarning("RespawnCharacter: session spawn point is null. " + "Fallback to team spawn point.");

            _spawnPoint = GetSpawnPoint(session.TeamId.Value);
        }

        if (_spawnPoint == null)
        {
            Debug.LogError("RespawnCharacter: no spawn point found.");

            return;
        }

        GameObject _characterObject =
            Instantiate(m_playerCharacterPrefab, _spawnPoint.transform.position, _spawnPoint.transform.rotation);

        NetworkObject _networkObject =
            _characterObject.GetComponent<NetworkObject>();

        if (_networkObject == null)
        {
            Debug.LogError("Player character prefab does not have a NetworkObject component.");

            return;
        }

        PlayerController _playerController = _characterObject.GetComponent<PlayerController>();

        if (_playerController == null)
        {
            Debug.LogError("Player prefab dont have any player controller.");

            return;
        }

        _networkObject.SpawnAsPlayerObject(session.ClientId.Value);

        _playerController.SessionObjectId.Value = session.NetworkObjectId;

        _playerController.Initialize(session);

        session.CharacterObjectId.Value = _networkObject.NetworkObjectId;

        session.SetSpawnPoint(_spawnPoint);

        session.SetCharacter(_playerController);

        session.MovePlayeToSpawnPoint(2.5f);

        _spawnPoint.IsOccupied = true;

        if (m_debugMode)
        {
            Debug.Log("Character respawned for client: " + session.ClientId.Value + " at spawn point: " + _spawnPoint.name);
        }
    }

    private void DespawnCharacter(PlayerSession session)
    {
        if (!IsServer)
        {
            return;
        }

        if (!SessionManager.Instance.m_playerSessions.ContainsKey(session.ClientId.Value))
        {
            Debug.LogError(session.ClientId.Value + " does not have a session.");
            return;
        }

        PlayerController _playerController = session.GetCharacter();

        if (_playerController == null)
        {
            Debug.LogError(session.ClientId.Value + " does not have a character to despawn.");
            return;
        }

        SpawnPoint _spawnPoint = _playerController.GetSession().GetSpawnPoint();

        if (_spawnPoint != null)
        {
            _spawnPoint.IsOccupied = false;
        }

        session.SetCharacter(null);

        _playerController.NetworkObject.Despawn(true);

        if (m_debugMode)
        {
            Debug.Log("Character despawned for client: " + session.ClientId.Value);
        }
    }

    public bool SpawnPlayers()
    {
        if (!IsServer)
        {
            return false;
        }

        foreach (var _playerSession in SessionManager.Instance.m_playerSessions)
        {
            if (_playerSession.Value.GetSessionRole() == SessionRole.Player &&
                GetSpawnPoint(_playerSession.Value.TeamId.Value) == null)
            {
                Debug.LogError("SpawnPlayers failed: no spawn point for team: " + _playerSession.Value.TeamId.Value);
                return false;
            }
        }

        AssignManeColors();

        foreach (var _playerSession in
                 SessionManager.Instance.m_playerSessions)
        {
            switch (_playerSession.Value.GetSessionRole())
            {
                case SessionRole.Player:
                    CreateBlock(_playerSession.Value);
                    SpawnCharacter(_playerSession.Value);
                    break;

                case SessionRole.Spectator:
                    break;
            }
        }

        BindPlayerCardWorldAreas();

        MultiplayerManager.Instance.m_currentMultiplayerStatus =
            MultiplayerStatus.InGameStatus;
        return true;
    }
    #endregion

    #region Block
    private void CreateBlock(PlayerSession session)
    {
        if (!IsServer)
        {
            return;
        }

        var _spawnPoint = GetSpawnPoint(session.TeamId.Value);
        if (_spawnPoint == null)
        {
            Debug.LogError("No Spawn Point");
            return;
        }
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                Vector3 _spawnPosition =
                _spawnPoint.transform.position + Vector3.down * 2.5f + Vector3.left * (-2 + j) * 3f + Vector3.forward * (2 - i) * 3f;
                GameObject _obj = Instantiate(m_blockPrefab, _spawnPosition, _spawnPoint.gameObject.transform.rotation);

                NetworkObject _netObj = _obj.GetComponent<NetworkObject>();
                if (_netObj == null)
                {
                    Debug.LogError("No network object");
                    return;
                }
                //todo
                //BlockController _blockController = _netObj.GetComponentInChildren<BlockController>();
                BlockController _blockController = _netObj.GetComponent<BlockController>();
                if (_blockController == null)
                {
                    Debug.LogError("Prefab is not a block");
                    return;
                }
                _netObj.SpawnWithOwnership(session.ClientId.Value, true);
                _blockController.Initialize(session);
                session.AddBlock(_blockController, i, j);
            }
        }

    }
    #endregion
}
