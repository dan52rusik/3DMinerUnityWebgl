using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace SimpleVoxelSystem.Net
{
    /// <summary>
    /// Runtime bootstrap for NGO networking.
    /// Creates/initializes NetworkManager and a runtime Player prefab cloned from current Player.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        private static NetworkBootstrap instance;

        public enum AutoStartMode
        {
            Disabled,
            Host,
            Client
        }

        [Header("Startup")]
        public AutoStartMode autoStart = AutoStartMode.Disabled;
        public bool showDebugGui = true;
        public bool runInBackground = true;

        [Header("Connection")]
        public string address = "127.0.0.1";
        public ushort port = 7777;
        public bool useWebSocketsForWebGL = true;

        private NetworkManager networkManager;
        private UnityTransport transport;
        private GameObject runtimePlayerPrefab;
        private GameObject offlinePlayer;

        private bool started;
        private bool offlineHiddenForNetwork;
        private bool callbacksSubscribed;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            if (runInBackground)
                Application.runInBackground = true;
        }

        void Update()
        {
            if (!started || networkManager == null)
                return;

            if (networkManager.IsListening)
            {
                // Дожидаемся фактического подключения к серверу (рукопожатие NGO),
                // чтобы безопасно получить LocalClient.
                if (networkManager.IsConnectedClient)
                {
                    if (!offlineHiddenForNetwork &&
                        networkManager.LocalClient != null &&
                        networkManager.LocalClient.PlayerObject != null)
                    {
                        DisableOfflinePlayer();
                        offlineHiddenForNetwork = true;
                    }
                }
                return;
            }

            // Recover from transport errors / failed connect attempts.
            if (offlineHiddenForNetwork)
            {
                RestoreOfflinePlayer();
                offlineHiddenForNetwork = false;
            }
            started = false;
        }

        System.Collections.IEnumerator Start()
        {
            // Ждем 1 кадр, чтобы WellGenerator.Start() успел сгенерировать остров 
            // и переместить оффлайн-игрока в правильную позицию спавна (в центр).
            yield return null;

            EnsureNetworkStack();

            if (autoStart == AutoStartMode.Disabled)
                yield break;

            if (autoStart == AutoStartMode.Host) StartHost();
            if (autoStart == AutoStartMode.Client) StartClient();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;

            UnsubscribeNetworkCallbacks();
            Shutdown();

            if (runtimePlayerPrefab != null)
            {
                if (runtimePlayerPrefab.transform.parent != null)
                    Destroy(runtimePlayerPrefab.transform.parent.gameObject);
                else
                    Destroy(runtimePlayerPrefab);
            }
        }

        void OnDisable()
        {
            // Protects against disabled-domain-reload mode where transport jobs may survive Play stop.
            Shutdown();
        }

        void OnApplicationQuit()
        {
            Shutdown();
        }

        public void StartHost()
        {
            EnsureNetworkStack();
            if (networkManager == null || networkManager.IsListening)
                return;

            ConfigureTransport();
            if (!networkManager.StartHost())
                RestoreOfflinePlayer();
            else
                started = true;
        }

        public void StartClient()
        {
            EnsureNetworkStack();
            if (networkManager == null || networkManager.IsListening)
                return;

            ConfigureTransport();
            if (!networkManager.StartClient())
                RestoreOfflinePlayer();
            else
                started = true;
        }

        public void StartServer()
        {
            EnsureNetworkStack();
            if (networkManager == null || networkManager.IsListening)
                return;

            ConfigureTransport();
            if (!networkManager.StartServer())
                RestoreOfflinePlayer();
            else
                started = true;
        }

        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();

            started = false;
            offlineHiddenForNetwork = false;
            RestoreOfflinePlayer();
        }

        void OnGUI()
        {
            if (!showDebugGui)
                return;

            const int w = 120;
            const int h = 28;
            int x = 10;
            int y = 120;

            if (networkManager != null && networkManager.IsListening)
            {
                string mode = networkManager.IsHost ? "HOST" : (networkManager.IsServer ? "SERVER" : "CLIENT");
                GUI.Label(new Rect(x, y - 22, 300, 22), $"NET: {mode}  addr={address}:{port}");
                if (GUI.Button(new Rect(x, y, w, h), "Disconnect"))
                    Shutdown();
                return;
            }

            GUI.Label(new Rect(x, y - 22, 300, 22), $"NET: OFF  addr={address}:{port}");
            if (GUI.Button(new Rect(x, y, w, h), "Start Host"))
                StartHost();
            if (GUI.Button(new Rect(x + w + 8, y, w, h), "Start Client"))
                StartClient();
        }

        private void EnsureNetworkStack()
        {
            if (networkManager == null)
                networkManager = FindFirstObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                GameObject go = new GameObject("NetworkManagerRuntime");
                DontDestroyOnLoad(go);
                networkManager = go.AddComponent<NetworkManager>();
            }

            if (transport == null)
                transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
                transport = networkManager.gameObject.AddComponent<UnityTransport>();

            if (networkManager.NetworkConfig == null)
                networkManager.NetworkConfig = new NetworkConfig();

            if (networkManager.NetworkConfig.NetworkTransport != transport)
                networkManager.NetworkConfig.NetworkTransport = transport;

            SubscribeNetworkCallbacks();
            EnsureRuntimePlayerPrefab();
        }

        private void SubscribeNetworkCallbacks()
        {
            if (networkManager == null || callbacksSubscribed)
                return;

            networkManager.OnTransportFailure += OnTransportFailure;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStopped += OnServerStopped;
            callbacksSubscribed = true;
        }

        private void UnsubscribeNetworkCallbacks()
        {
            if (networkManager == null || !callbacksSubscribed)
                return;

            networkManager.OnTransportFailure -= OnTransportFailure;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStopped -= OnServerStopped;
            callbacksSubscribed = false;
        }

        private void OnTransportFailure()
        {
            Shutdown();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (networkManager == null)
                return;

            // If local side got disconnected, stop transport immediately.
            if (clientId == networkManager.LocalClientId)
                Shutdown();
        }

        private void OnServerStopped(bool _)
        {
            Shutdown();
        }

        private void EnsureRuntimePlayerPrefab()
        {
            if (runtimePlayerPrefab != null)
            {
                networkManager.NetworkConfig.PlayerPrefab = runtimePlayerPrefab;
                return;
            }

            GameObject source = GameObject.FindGameObjectWithTag("Player");
            if (source == null)
            {
                PlayerCharacterController pcc = FindFirstObjectByType<PlayerCharacterController>();
                if (pcc != null) source = pcc.gameObject;
            }

            if (source == null)
            {
                Debug.LogWarning("[NetworkBootstrap] Player template not found in scene.");
                return;
            }

            // Keep a stable reference to scene/offline player before networking starts.
            if (offlinePlayer == null)
                offlinePlayer = source;

            GameObject prefabHolder = new GameObject("NetworkPrefabHolder");
            DontDestroyOnLoad(prefabHolder);
            prefabHolder.SetActive(false);

            runtimePlayerPrefab = Instantiate(source, prefabHolder.transform);
            runtimePlayerPrefab.name = "NetworkPlayerPrefabRuntime";
            runtimePlayerPrefab.tag = "Untagged";

            if (runtimePlayerPrefab.GetComponent<NetworkObject>() == null)
                runtimePlayerPrefab.AddComponent<NetworkObject>();
            if (runtimePlayerPrefab.GetComponent<ClientNetworkTransform>() == null)
                runtimePlayerPrefab.AddComponent<ClientNetworkTransform>();
            if (runtimePlayerPrefab.GetComponent<NetPlayerAvatar>() == null)
                runtimePlayerPrefab.AddComponent<NetPlayerAvatar>();
            if (runtimePlayerPrefab.GetComponent<NetWorldPresenceSync>() == null)
                runtimePlayerPrefab.AddComponent<NetWorldPresenceSync>();

            networkManager.NetworkConfig.PlayerPrefab = runtimePlayerPrefab;
        }

        private void ConfigureTransport()
        {
            if (transport == null)
                return;

            transport.SetConnectionData(address, port);
#if UNITY_WEBGL && !UNITY_EDITOR
            transport.UseWebSockets = useWebSocketsForWebGL;
#endif
        }

        private void DisableOfflinePlayer()
        {
            if (offlinePlayer == null)
                offlinePlayer = FindOfflinePlayerCandidate();

            if (offlinePlayer != null)
                offlinePlayer.SetActive(false);
        }

        private void RestoreOfflinePlayer()
        {
            if (offlinePlayer != null)
                offlinePlayer.SetActive(true);
        }

        private GameObject FindOfflinePlayerCandidate()
        {
            var controllers = FindObjectsByType<PlayerCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in controllers)
            {
                if (c == null) continue;
                var no = c.GetComponent<NetworkObject>();
                if (no == null)
                    return c.gameObject;
            }

            // Fallback for legacy scenes.
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.GetComponent<NetworkObject>() == null)
                return tagged;

            return null;
        }
    }
}
