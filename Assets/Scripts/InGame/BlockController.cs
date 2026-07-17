using Unity.Netcode;
using UnityEngine;

public class BlockController : NetworkBehaviour, ISelectable
{
    #region Variable
    [SerializeField] private GameObject m_spawnPoint;
    [SerializeField] private Renderer m_renderer;

    private SpawnPoint m_spawn;

    public NetworkVariable<int> m_teamId = new NetworkVariable<int>();
    public NetworkVariable<int> m_blockHP = new NetworkVariable<int>(100,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);

    public bool m_debugMode = false;

    private bool m_setSelected = false;

    private PlayerSession m_ownerSession = null;

    public NetworkVariable<int> TeamId = new NetworkVariable<int>();

    #endregion

    #region Spawn/Despawn function
    public override void OnNetworkSpawn()
    {
        m_blockHP.OnValueChanged += OnHpChanged;
        m_renderer = GetComponent<Renderer>();
    }
    public override void OnNetworkDespawn()
    {
        Debug.Log("DespawnBlock");
        m_blockHP.OnValueChanged -= OnHpChanged;
    }

    private void OnHpChanged(int previous, int current)
    {
        if(m_debugMode)
        {
            Debug.Log("HP changed {previous} to {current}");
        }
    }

    public void Initialize(PlayerSession session, int hpmax = 100)
    {
        if(!IsServer)
        {
            return;
        }
        if(m_debugMode)
        {

            Debug.Log("Block init called");
        }

        m_teamId.Value = session.TeamId.Value;

        m_blockHP.Value = hpmax;

        if(m_blockHP.Value<0)
        {
            m_blockHP.Value = 0;
        }
    }
    #endregion

    public void SetSelected(bool selectedState)
    {
        m_setSelected = selectedState;

        if (m_setSelected)
        {
            m_renderer.material.color = Color.white;
        }
        else
        {
            m_renderer.material.color = Color.gray;
        }
    }
    public void SetSession(PlayerSession session)
    {
        m_ownerSession = session;
        m_spawn = this.GetComponentInChildren<SpawnPoint>(true);
        if(m_spawn != null)
        {
            m_spawn.Initialize(m_ownerSession.TeamId.Value);
            TeamId.Value = m_ownerSession.TeamId.Value;
        }
    }
    public PlayerSession GetSession()
    {
        return m_ownerSession;
    }
    public SpawnPoint GetSpawn()
    {
        if(m_spawn == null)
        {
            m_spawn = this.GetComponentInChildren<SpawnPoint>();
            if(m_spawn != null)
            {
                m_spawn.Initialize(TeamId.Value);
            }
        }
        return m_spawn;
    }
}
