using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public partial class GameMaster
{
    private void InputQueueUpdate()
    {
        while (m_inputQueue.Count > 0)
        {
            PlayerControlPackage _package = m_inputQueue.Dequeue();
            //Package is valid, we can process it and send the result back to the client
            ToPlayerControlPackage _packageNew = new ToPlayerControlPackage
            {
                RequestId = _package.RequestId,
                ClientId = _package.ClientId,
                s_horizontal = _package.s_horizontal,
                s_vertical = _package.s_vertical,
                s_enabled = true
            };
            if (m_debugMode) Debug.Log("Handle Packege in Update");

            //We create the ClientRpcParams to specify which client should receive the result of the processed package
            ClientRpcParams _rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[]
                                    {
                                        _package.ClientId
                                    }
                }

            };
            //Only process movement for connected clients.
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(_package.ClientId))
            {
                if (m_debugMode)
                {
                    Debug.Log($"Client disconnected: {_package.ClientId}");
                }

                continue;
            }
            PlayerSession _session = SessionManager.Instance != null
                ? SessionManager.Instance.GetPlayerSession(_package.ClientId)
                : null;

            if (_session == null)
            {
                if (m_debugMode)
                {
                    Debug.Log("Movement input rejected: no player session.");
                }

                continue;
            }

            if (!CanPlayerMove(_session))
            {
                if (m_debugMode)
                {
                    Debug.LogWarning("Movement input rejected: player cannot move in the current phase or turn.");
                }

                continue;
            }

            PlayerController _playerController = _session.GetCharacter();

            if (_playerController == null || _playerController.OwnerClientId != _package.ClientId)
            {
                continue;
            }

            MoveInputProcessing(_playerController, _package);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_debugMode)
            {
                HPDeltaProcessing(_playerController, _package);
            }
#endif

            //Send Package back to the client to confirm that the input was processed and to update the client's state if necessary
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(_package.ClientId))
            {
                continue;
            }
            SendToClientRpc(_packageNew, _rpcParams);
        }
    }

    private bool CheckActivity(CardActivity activity, ulong targetID, ulong cardOwnerId)
    {
        bool _result = false;
        var _ownerPlayer = GetSessionByID(cardOwnerId);

        if (_ownerPlayer == null)
        {
            Debug.LogError("No owner");
            return false;
        }
        var _ownerPlayerOponent = GetSessionByID(_ownerPlayer.m_currentOponentNetworkObjectId.Value);
        PlayerController _targetCharacter = null;
        var _targetSession = GetSessionByID(targetID);
        if (_targetSession != null)
        {
            _targetCharacter = GetSessionByID(targetID).GetCharacter();
        }
        BlockController _targetBlock = GetBlockById(targetID);
        switch (activity.s_targetType)
        {
            //todo
            case CardTargetType.AllBlockAnyPlayer:
            case CardTargetType.AllPlayer:
            case CardTargetType.AllBlockSelf:
            case CardTargetType.AllBlockOpponent:
            case CardTargetType.RandomBlockSelectedPlayer:
            case CardTargetType.RandomOpponentBlock:
            case CardTargetType.RandomSelfBlock:
                _result = true;
                break;
            case CardTargetType.AnyPlayer:

                if (_targetCharacter != null) _result = true;
                break;
            case CardTargetType.AnyPlayerBlock:
                if (_targetBlock != null) _result = true;
                break;
            case CardTargetType.OpponentBlock:
                _result = _targetBlock != null && _ownerPlayerOponent != null &&
                    _targetBlock.m_teamId.Value == _ownerPlayerOponent.TeamId.Value;
                break;
            case CardTargetType.OpponentPlayer:
                if (_targetCharacter != null && _ownerPlayerOponent != null &&
                    _ownerPlayerOponent.GetCharacter() == _targetCharacter)
                {
                    _result = true;
                }
                else
                {
                    Debug.Log("Error");
                }
                break;
            case CardTargetType.SelfBlock:
                _result = _targetBlock != null &&
                    _targetBlock.m_teamId.Value == _ownerPlayer.TeamId.Value;
                break;
            case CardTargetType.SelfPlayer:
                if (_targetCharacter != null && _ownerPlayer.GetCharacter() == _targetCharacter)
                {
                    _result = true;
                }
                break;
            case CardTargetType.None:
                _result = true;
                break;
        }

        return _result;
    }
    //todo for all
    private void ProcessActivity(CardActivity activity, ulong targetID, ulong cardOwnerId)
    {
        Debug.Log("Process called");
        PlayerController _ownerPlayer = GetSessionByID(cardOwnerId).GetCharacter();
        PlayerController _targetCharacter = null;
        var _targetSession = GetSessionByID(targetID);
        if (_targetSession != null)
        {
            _targetCharacter = GetSessionByID(targetID).GetCharacter();
        }
        BlockController _targetBlock = GetBlockById(targetID);
        switch (activity.s_actionType)
        {
            case CardActionType.None:
                break;
            case CardActionType.Damage:
                if (_targetCharacter != null)
                    if (activity.s_targetType == CardTargetType.AnyPlayer || activity.s_targetType == CardTargetType.OpponentPlayer || activity.s_targetType == CardTargetType.SelfPlayer)
                    {
                        _targetCharacter.m_hpCurrent.Value -= activity.s_value;
                        UpdatePlayerHP(_targetCharacter);
                    }
                    else
                    {
                        Debug.LogWarning("Error here");
                    }

                if (_targetBlock != null)
                    if (activity.s_targetType == CardTargetType.AnyPlayerBlock || activity.s_targetType == CardTargetType.OpponentBlock || activity.s_targetType == CardTargetType.SelfBlock)
                    {
                        _targetBlock.m_blockHP.Value -= activity.s_value;
                        UpdateBlockHp(_targetBlock);
                    }
                break;
            case CardActionType.Heal:
                if (_targetCharacter != null)
                    if (activity.s_targetType == CardTargetType.AnyPlayer || activity.s_targetType == CardTargetType.OpponentPlayer || activity.s_targetType == CardTargetType.SelfPlayer)
                    {
                        if (_targetCharacter.m_hpCurrent.Value <= (_targetCharacter.m_hpMax.Value - activity.s_value))
                        {
                            _targetCharacter.m_hpCurrent.Value += activity.s_value;
                        }
                        else
                        {
                            double _shieldDelta = (activity.s_value - _targetCharacter.m_hpMax.Value + _targetCharacter.m_hpCurrent.Value) * 0.5;
                            _targetCharacter.m_hpCurrent.Value = _targetCharacter.m_hpMax.Value + (_shieldDelta).ConvertTo<int>();
                        }
                        UpdatePlayerHP(_targetCharacter);
                    }
                //todo
                if (_targetBlock != null)
                    if (activity.s_targetType == CardTargetType.AnyPlayerBlock || activity.s_targetType == CardTargetType.OpponentBlock || activity.s_targetType == CardTargetType.SelfBlock)
                    {

                        int _blockMaxHp = _targetBlock.GetMaxHP();
                        if (_targetBlock.m_blockHP.Value <= (_blockMaxHp - activity.s_value))
                        {
                            _targetBlock.m_blockHP.Value += activity.s_value;
                        }
                        else
                        {
                            double _shieldDelta = (activity.s_value - _blockMaxHp + _targetBlock.m_blockHP.Value) * 0.5;
                            _targetBlock.m_blockHP.Value = _blockMaxHp + (_shieldDelta).ConvertTo<int>();
                        }
                        UpdateBlockHp(_targetBlock);
                    }
                break;
            case CardActionType.DrawCard:
                for (int i = 0; i < activity.s_value; i++)
                {
                    DrawCard(_targetSession);
                }
                break;
            //todo
            case CardActionType.DiscardCardHeal:
            case CardActionType.DiscardCard:
            case CardActionType.DiscardCardDamage:
            case CardActionType.DiscardCardDrawCard:
            case CardActionType.DrawCardDamage:
            case CardActionType.DrawCardDiscardCard:
            case CardActionType.DrawCardHeal:
            case CardActionType.DrawCardDrawCard:
            case CardActionType.DiscardCardDiscardCard:
                break;
        }
    }
    private void MoveInputProcessing(PlayerController controller, PlayerControlPackage _package)
    {
        if (controller == null)
            return;

        int _horizontal =
            Mathf.RoundToInt(_package.s_horizontal);

        int _vertical =
            Mathf.RoundToInt(_package.s_vertical);

        if (!IsValidStepDirection(_horizontal, _vertical))
        {
            Debug.LogWarning(
                "MoveInputProcessing rejected: invalid direction. " +
                "Horizontal: " + _horizontal +
                " Vertical: " + _vertical);

            return;
        }

        Vector3 _direction =
            new Vector3(
                _horizontal,
                0f,
                _vertical);

        Vector3 _delta =
            _direction * m_stepMoveDistance;

        Rigidbody _rigidbody =
            controller.GetRigidbody();

        if (_rigidbody != null)
        {
            Vector3 _targetPosition =
                _rigidbody.position + _delta;

            _targetPosition.y =
                _rigidbody.position.y;

            _rigidbody.position =
                _targetPosition;

            Vector3 _velocity =
                _rigidbody.linearVelocity;

            _rigidbody.linearVelocity =
                new Vector3(
                    0f,
                    _velocity.y,
                    0f);
        }
        else
        {
            Vector3 _targetPosition =
                controller.transform.position + _delta;

            _targetPosition.y =
                controller.transform.position.y;

            controller.transform.position =
                _targetPosition;
        }

        if (m_debugMode)
        {
            Debug.Log(
                "Step moved player. Delta: " +
                _delta +
                " New position: " +
                controller.transform.position);
        }
    }
    private void HPDeltaProcessing(PlayerController controller, PlayerControlPackage _package)
    {
        if (_package.s_hpDelta != 0)
        {
            if (m_debugMode) Debug.Log($"CurrentHP before delta: {controller.GetCurrentHP()}");
            controller.m_hpCurrent.Value += _package.s_hpDelta;
            if (m_debugMode) Debug.Log($"HPDelta: {_package.s_hpDelta}");
            {
                UpdatePlayerHP(controller);
            }
        }
    }
    private void UpdatePlayerHP(PlayerController controller)
    {
        if (controller.m_hpCurrent.Value <= 0)
        {
            var _session = controller.GetSession();
            if (_session == null)
            {
                Debug.LogError("UpdatePlayerHP: character has no session.");
                return;
            }
            _session.m_lifeCurrent.Value -= 1;

            DespawnCharacter(_session);

            SpawnPoint _spawnPoint = _session.GetSpawnPoint();
            if (_spawnPoint != null)
            {
                _spawnPoint.IsOccupied = false;
            }

            _session.SetCharacter(null);
            _session.CharacterObjectId.Value = 0;
        }
    }
    private void UpdateBlockHp(BlockController controller)
    {
        if (controller == null)
            return;

        if (controller.m_blockHP.Value > 0)
            return;

        PlayerSession _session = controller.GetSession();

        if (_session != null)
        {
            _session.DeleteBlock(controller);
        }

        if (controller.NetworkObject != null &&
            controller.NetworkObject.IsSpawned)
        {
            controller.NetworkObject.Despawn(true);
        }
    }
    public void ServerKillPlayerByHazard(PlayerController playerController)
    {
        if (!IsServer)
            return;

        if (playerController == null)
            return;

        PlayerSession _session =
            playerController.GetSession();

        if (_session == null)
            return;

        if (_session.GetCharacter() != playerController)
            return;

        if (_session.m_lifeCurrent.Value <= 0)
            return;

        playerController.m_hpCurrent.Value = 0;

        UpdatePlayerHP(playerController);

        m_externalDeathDetected = true;

        if (m_debugMode)
        {
            Debug.Log(
                "GameMaster: player killed by hazard. ClientId: " +
                _session.ClientId.Value +
                " Life left: " +
                _session.m_lifeCurrent.Value);
        }
    }
    private bool IsValidStepDirection(int horizontal, int vertical)
    {
        if (horizontal < -1 || horizontal > 1)
            return false;

        if (vertical < -1 || vertical > 1)
            return false;

        int _sum =
            Mathf.Abs(horizontal) +
            Mathf.Abs(vertical);

        return _sum == 1;
    }
}
