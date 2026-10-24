using Unity.Netcode;
using UnityEngine;

public class PonyColorScript : NetworkBehaviour
{
    [SerializeField] private GameObject bodyObject;
    [SerializeField] private GameObject maneObject;

    private Renderer bodyRenderer;
    private Renderer maneRenderer;

    bool m_isSelected = false;

    [SerializeField] private Material[] m_materials;
    [SerializeField] private Material[] m_maneMaterials;

    public NetworkVariable<int> NetworkBodyColor = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    
    public NetworkVariable<int> NetworkManeColor = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (bodyObject != null)
        {
            bodyRenderer = bodyObject.GetComponent<Renderer>();
        }
        else
        {
            Debug.LogWarning("Body object is not assigned in the inspector.");
        }
        if (maneObject != null)
        {
            maneRenderer = maneObject.GetComponent<Renderer>();
        }
        else
        {
            Debug.LogWarning("Mane object is not assigned in the inspector.");
        }
    }
    public void Start()
    {
        SetMaterials(NetworkBodyColor.Value, NetworkManeColor.Value);
    }
    public void SetMaterials(int bodyMaterialIndex, int maneMaterialIndex)
    {
        if (bodyRenderer != null &&
            bodyMaterialIndex >= 0 &&
            bodyMaterialIndex < m_materials.Length)
        {
            var mats = bodyRenderer.materials;

            if (mats.Length > 0)
            {
                mats[0] = m_materials[bodyMaterialIndex];
                if(IsServer)NetworkBodyColor.Value = bodyMaterialIndex;
                bodyRenderer.materials = mats;
            }

        }
        else
        {
            Debug.LogWarning("Invalid body material index.");
        }

        if (maneRenderer != null &&
            maneMaterialIndex >= 0 &&
            maneMaterialIndex < m_maneMaterials.Length)
        {
            var mats = maneRenderer.materials;

            if (mats.Length > 0)
            {
                mats[0] = m_maneMaterials[maneMaterialIndex];
                if(IsServer)NetworkManeColor.Value = maneMaterialIndex;
                maneRenderer.materials = mats;
            }

        }
        else if (maneRenderer != null)
        {
            Debug.LogWarning("Invalid mane material index.");
        }
    }


    public override void OnNetworkSpawn()
    {
        NetworkBodyColor.OnValueChanged += OnBodyColorChanged;
        NetworkManeColor.OnValueChanged += OnManeColorChanged;

        ApplyBodyMaterial(NetworkBodyColor.Value);
        ApplyManeMaterial(NetworkManeColor.Value);
    }

    public override void OnNetworkDespawn()
    {
        NetworkBodyColor.OnValueChanged -= OnBodyColorChanged;
        NetworkManeColor.OnValueChanged -= OnManeColorChanged;
    }

    private void OnBodyColorChanged(int previousValue, int newValue)
    {
        ApplyBodyMaterial(newValue);
    }

    private void OnManeColorChanged(int previousValue, int newValue)
    {
        ApplyManeMaterial(newValue);
    }

    private void ApplyBodyMaterial(int materialIndex)
    {
        if (bodyRenderer == null)
            return;

        if (materialIndex < 0 || materialIndex >= m_materials.Length)
            return;

        Material[] mats = bodyRenderer.materials;

        if (mats.Length <= 0)
            return;

        mats[0] = m_materials[materialIndex];
        bodyRenderer.materials = mats;
    }

    private void ApplyManeMaterial(int materialIndex)
    {
        if (maneRenderer == null)
            return;

        if (materialIndex < 0 || materialIndex >= m_maneMaterials.Length)
            return;

        Material[] mats = maneRenderer.materials;

        if (mats.Length <= 0)
            return;

        mats[0] = m_maneMaterials[materialIndex];
        maneRenderer.materials = mats;
    }

    public void SetSelected(bool selected)
    {
        m_isSelected = selected;

        if (m_isSelected)
        {
            if (bodyRenderer != null)
                bodyRenderer.material.color = Color.white;

            if (maneRenderer != null)
                maneRenderer.material.color = Color.white;
        }
        else
        {
            ApplyBodyMaterial(NetworkBodyColor.Value);
            ApplyManeMaterial(NetworkManeColor.Value);
        }
    }
}
