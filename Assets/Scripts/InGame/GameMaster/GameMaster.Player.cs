using Unity.Netcode;
using UnityEngine;

public partial class GameMaster
{
    /// <summary>
    /// Returns the PlayerSession component of the NetworkObject with the given networkObjectId.
    /// </summary>
    /// <param name="networkObjectId">The network object ID of the player session.</param>
    /// <returns>The PlayerSession component if found; otherwise, null.</returns>
    public PlayerSession GetSessionByID(ulong networkObjectId)
    {
        PlayerSession _result = null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject _networkObject))
        {
            try
            {
                _result = _networkObject.GetComponent<PlayerSession>();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        return _result;
    }

    //todo
    /*
    private PlayerController GetCharacterByID(ulong networkObjectId)
    {
        PlayerController _result = null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject _networkObject))
        {
            try
            {
                _result = _networkObject.GetComponent<PlayerController>();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        return _result;
    }
    */
}
