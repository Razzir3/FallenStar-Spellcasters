using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LavaKillZone : MonoBehaviour
{
    #region Variable

    [SerializeField]
    private bool m_debugMode = false;

    private readonly HashSet<PlayerController> m_killedCharacters =
        new HashSet<PlayerController>();

    #endregion

    #region UnityCallingFunc

    private void Awake()
    {
        m_debugMode = DebugModeGuard.Allow(m_debugMode);
        Collider _collider =
            GetComponent<Collider>();

        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryKillPlayer(other);
    }

    #endregion

    #region KillFunc

    private void TryKillPlayer(Collider other)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        if (GameMaster.Instance == null)
            return;

        PlayerController _playerController =
            other.GetComponentInParent<PlayerController>();

        if (_playerController == null)
            return;

        if (_playerController.NetworkObject == null)
            return;

        if (!_playerController.NetworkObject.IsSpawned)
            return;

        m_killedCharacters.RemoveWhere(c => c == null);

        if (m_killedCharacters.Contains(
                _playerController))
        {
            return;
        }

        PlayerSession _session =
            _playerController.GetSession();

        if (_session == null)
            return;

        if (_session.GetCharacter() != _playerController)
            return;

        m_killedCharacters.Add(
            _playerController);

        if (m_debugMode)
        {
            Debug.Log(
                "LavaKillZone: killing player. ClientId: " +
                _session.ClientId.Value);
        }

        GameMaster.Instance.ServerKillPlayerByHazard(
            _playerController);
    }

    #endregion
}
