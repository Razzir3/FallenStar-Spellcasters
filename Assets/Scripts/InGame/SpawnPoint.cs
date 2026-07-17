using Unity.Netcode;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{

    [SerializeField]
    private int m_teamId = -1;

    public int TeamId => m_teamId;

    [SerializeField]
    private bool m_isEnabled = true;

    private bool m_isOccupied = false;

    public bool IsEnabled => m_isEnabled;

    public bool IsOccupied
    {
        get => m_isOccupied;
        set => m_isOccupied = value;
    }
    public void Initialize(int teamId)
    {
        m_teamId = teamId;
    }
}
