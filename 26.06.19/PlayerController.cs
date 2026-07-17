using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
//using UnityEditor.Search;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;


    public class PlayerController : NetworkBehaviour, ISelectable
{
        private float m_moveSpeedBase = 0.0f;
        public NetworkVariable<float> m_moveSpeedCurrent = new NetworkVariable<float>(0,NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private int m_requestId = 0;

        public NetworkVariable<int> m_hpMax = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> m_hpCurrent = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


        public NetworkVariable<bool> m_isDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> m_playerTeam = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Queue<ToPlayerControlPackage> m_inputQueue;

        [SerializeField] private Renderer m_meshRenderer;
    private Color m_defaultColor;

        private PonyColorScript m_ponyColorScript;

    private PlayerSession m_session;

        public NetworkVariable<ulong> SessionObjectId = new(0);

        public bool m_isSelected = false;


    //ToDo
    //private bool m_deadProccessed = false;

    public bool m_debugMode = false;

        public override void OnNetworkSpawn()
        {
            StartCoroutine(DelayedInit());

            if(m_debugMode)
            {
                Debug.Log("Spawned. Owner :{IsOwner} ClientId : {OwnerClientId}");
            }

            if(IsOwner)
            {
                if(m_debugMode)
                {
                    Debug.Log("Local player assigned");
                }
            }
        }

        public void SetSelected(bool selectedState)
        {
            m_isSelected = selectedState;
            if(m_isSelected)
            {
                m_ponyColorScript.SetSelected(true);
            }
            else
            {
                m_ponyColorScript.SetSelected(false);
            }
        }

        private IEnumerator DelayedInit()
        {
            yield return null;

            if (m_debugMode) Debug.Log($"FINAL OwnerClientId: {OwnerClientId}");
            m_ponyColorScript = GetComponent<PonyColorScript>();
        if (IsServer)
            {
                Init();
            }

            while(GetSession() == null)
            {
                yield return null;
            }
            ApplyMaterial();

        }

        private void ApplyMaterial()
        {
            int _index = m_session.TeamId.Value;

            if (m_debugMode) Debug.Log($"Applying material index: {_index} for OwnerClientId: {OwnerClientId}");
            m_ponyColorScript.SetMaterials(_index, _index);
            m_defaultColor = m_meshRenderer.material.color;
        }
        public float GetCurrentSpeed()
        {
            return m_moveSpeedCurrent.Value;
        }
        public int GetCurrentHP()
        {
            return (int)m_hpCurrent.Value;
        }
    private void Awake()
    {
        if (m_meshRenderer == null)
        {
            m_meshRenderer = GetComponent<Renderer>();
        }
    }
    void Init()
        {
            m_moveSpeedBase = 3.0f;
            m_moveSpeedCurrent.Value = m_moveSpeedBase;

            m_hpMax.Value = 100;

            m_hpCurrent.Value = m_hpMax.Value;

            m_inputQueue = new Queue<ToPlayerControlPackage>();
            
    }
        private void Update()
        {
            //ToDo
            /*
            if(m_deadProccessed)
            {
                Destroy(gameObject);
            }
            if(m_isDead.Value)
            {
                m_deadProccessed = true;
            }
            */
            if (!IsOwner)
            {
                return;
            }
            SendMovementInput();
        }
        private void SendMovementInput()
        {

            float _horizontal = 0;
            float _vertical = 0;
            int _hpDelta = 0;

            //input
            {
                if (Keyboard.current.wKey.wasPressedThisFrame)
                {
                    _vertical += 1;
                }
                if (Keyboard.current.sKey.wasPressedThisFrame)
                {
                    _vertical -= 1;
                }
                if (Keyboard.current.aKey.wasPressedThisFrame)
                {
                    _horizontal -= 1;
                }
                if (Keyboard.current.dKey.wasPressedThisFrame)
                {
                    _horizontal += 1;
                }

                if(Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    _hpDelta -= 10;
                }
            }

        //If there is any input, send it to the server
        if (_horizontal != 0 || _vertical != 0 || _hpDelta != 0)
        {
            if (GameMaster.Instance.CanPlayerMove(m_session))
            {
                //Create the package to send to the server
                PlayerControlPackage _package = new PlayerControlPackage
                {
                    RequestId = m_requestId++,
                    ClientId = NetworkManager.Singleton.LocalClientId,
                    s_horizontal = _horizontal,
                    s_vertical = _vertical,
                    s_hpDelta = (int)_hpDelta
                };
                if (m_debugMode)
                {
                    Debug.Log($"Sending Input Package: RequestId: {_package.RequestId}, ClientId: {_package.ClientId}, Horizontal: {_package.s_horizontal}, Vertical: {_package.s_vertical}");
                    Debug.Log($"IsServer:{IsServer} " + $"IsClient:{IsClient} " + $"IsOwner:{IsOwner}");
                }

                InputServerRpc(_package);
            }
        }

        }
        [ServerRpc]
        public void InputServerRpc(PlayerControlPackage _package)
        {
            try
            {
                if (m_debugMode) Debug.Log("RPC CALLED");

                if (GameMaster.Instance == null)
                {
                    Debug.LogError("GameMaster INSTANCE NULL");
                    return;
                }

                if (m_debugMode) Debug.Log("GameMaster INSTANCE OK");

                //Send package to GameMasterInstance
                bool _handleResult = GameMaster.Instance.HandlePlayerControlPackage(_package);

                if ((m_debugMode))
                {
                    Debug.Log($"RequestId: {_package.RequestId}");

                    if (_handleResult)
                    {
                        Debug.Log("HANDLE FINISHED");
                    }
                    else
                        Debug.Log("Handle unsuccess");
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }

        //This function is called by the GameMasterInstance when it has validated the input package
        public void ReceiveValidatedInput(ToPlayerControlPackage package)
        {
            m_inputQueue.Enqueue(package);
            if (m_debugMode)
            {
                Debug.Log("ReceiveValidatedInput");
                Debug.Log(m_moveSpeedCurrent);
                Debug.Log(Time.deltaTime);
            }
            //example of how to use the package 
            //better in new function that is called in update, but for testing purposes we will use it here
            //transform.Translate(new Vector3(package.s_horizontal, 0, package.s_vertical) * m_moveSpeed * Time.deltaTime);

        }

        public void Initialize(PlayerSession session)
        {
            m_session = session;
            m_playerTeam.Value = m_session.TeamId.Value;
            if(m_debugMode) Debug.Log("Attemp "+m_playerTeam.Value+" to player");
        }

        public PlayerSession GetSession()
        {
            if (m_session == null)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(SessionObjectId.Value, out NetworkObject _obj))
                {
                    m_session = _obj.GetComponent<PlayerSession>();
                }

            }
            return m_session;
        }


    }
