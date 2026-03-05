using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class LobbyRealtimeSync : MonoBehaviour
    {
        [Header("Shared Lobby Sync")]
        public bool enableSync = true;
        [Tooltip("HTTPS endpoint of lobby_sync.php. Leave empty to disable server sync.")]
        public string endpointUrl = string.Empty;
        public string roomId = "global_lobby";
        [Min(0.1f)] public float syncInterval = 0.35f;
        [Min(0.05f)] public float stateSendInterval = 0.15f;
        [Min(2f)] public float ghostTimeoutSeconds = 10f;
        [Range(8, 256)] public int maxOpsPerRequest = 64;
        public bool verboseLogs = false;

        public bool UseAuthoritativeServerState => enableSync && !string.IsNullOrWhiteSpace(ResolveEndpoint());

        private const string EndpointPrefKey = "svs_sync_endpoint";
        private const string ClientIdPrefKey = "svs_sync_client_id";
        private const string ClientNamePrefKey = "svs_sync_client_name";

        private LobbyEditor lobbyEditor;
        private WellGenerator wellGenerator;
        private Transform localPlayer;
        private string cachedEndpoint = string.Empty;
        private string clientId = string.Empty;
        private string clientName = "Player";
        private long lastSeq;
        private bool identityRequested;
        private bool snapshotApplied;
        private float nextStateSendAt;

        private readonly List<OpPacket> pendingOps = new List<OpPacket>();
        private readonly HashSet<string> seenOpIds = new HashSet<string>();
        private readonly Dictionary<string, GhostView> ghosts = new Dictionary<string, GhostView>();
        // FIX #4: ограничиваем память seenOpIds — не более 2000 последних ID
        private readonly Queue<string> seenOpIdsOrder = new Queue<string>();
        private const int MaxSeenOpIds = 2000;
        // FIX #5: один шаред материал для всех призраков — не создаём новый каждый раз
        private static Material _ghostSharedMaterial;

        [Serializable]
        private class SyncRequest
        {
            public string action;
            public string roomId;
            public string clientId;
            public string clientName;
            public long sinceSeq;
            public bool requestSnapshot;
            public PlayerStatePacket state;
            public OpPacket[] ops;
        }

        [Serializable]
        private class SyncResponse
        {
            public bool ok;
            public string error;
            public long seq;
            public OpPacket[] ops;
            public SnapshotCell[] snapshot;
            public PlayerStatePacket[] players;
        }

        [Serializable]
        private class SnapshotCell
        {
            public int x;
            public int y;
            public int z;
            public int blockType;
        }

        [Serializable]
        private class OpPacket
        {
            public string opId;
            public string from;
            public string kind;
            public int x;
            public int y;
            public int z;
            public int blockType;
            public long t;
        }

        [Serializable]
        private class PlayerStatePacket
        {
            public string clientId;
            public string name;
            public float x;
            public float y;
            public float z;
            public float ry;
            public bool inLobby;
            public long t;
        }

        [Serializable]
        private class IdentityPayload
        {
            public string playerId;
            public string playerName;
        }

        private class GhostView
        {
            public string id;
            public GameObject root;
            public Transform tr;
            public TextMesh label;
            public float lastSeenAt;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void LobbySync_RequestIdentity(string gameObjectName, string callbackMethod);
#endif

        private void Awake()
        {
            lobbyEditor = GetComponent<LobbyEditor>();
            EnsureClientIdentity();
            RequestIdentityFromWeb();
        }

        private void Start()
        {
            StartCoroutine(SyncLoop());
        }

        private void Update()
        {
            CleanupStaleGhosts();
        }

        private void OnDisable()
        {
            DestroyAllGhosts();
        }

        public void NotifyLocalPlace(Vector3Int pos, BlockType blockType)
        {
            if (!UseAuthoritativeServerState)
                return;

            EnqueueOp(new OpPacket
            {
                opId = NewOpId(),
                from = clientId,
                kind = "place",
                x = pos.x,
                y = pos.y,
                z = pos.z,
                blockType = (int)blockType,
                t = EpochMs()
            });
        }

        public void NotifyLocalRemove(Vector3Int pos)
        {
            if (!UseAuthoritativeServerState)
                return;

            EnqueueOp(new OpPacket
            {
                opId = NewOpId(),
                from = clientId,
                kind = "remove",
                x = pos.x,
                y = pos.y,
                z = pos.z,
                blockType = 0,
                t = EpochMs()
            });
        }

        public void OnIdentityResolved(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            IdentityPayload payload = null;
            try { payload = JsonUtility.FromJson<IdentityPayload>(json); }
            catch { }

            if (payload == null)
                return;

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(payload.playerId))
            {
                clientId = payload.playerId.Trim();
                PlayerPrefs.SetString(ClientIdPrefKey, clientId);
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(payload.playerName))
            {
                clientName = payload.playerName.Trim();
                PlayerPrefs.SetString(ClientNamePrefKey, clientName);
                changed = true;
            }

            if (changed)
                PlayerPrefs.Save();
        }

        private IEnumerator SyncLoop()
        {
            while (true)
            {
                if (UseAuthoritativeServerState && EnsureReadyForSync())
                {
                    yield return SendSyncRequest();
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, syncInterval));
            }
        }

        private bool EnsureReadyForSync()
        {
            if (lobbyEditor == null)
                lobbyEditor = GetComponent<LobbyEditor>();
            if (lobbyEditor == null)
                return false;

            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (wellGenerator == null || !wellGenerator.IsInLobbyMode)
                return false;

            if (string.IsNullOrWhiteSpace(clientId))
                EnsureClientIdentity();

            return true;
        }

        private IEnumerator SendSyncRequest()
        {
            string endpoint = ResolveEndpoint();
            if (string.IsNullOrWhiteSpace(endpoint))
                yield break;

            OpPacket[] opsChunk = DequeueOpsChunk();
            PlayerStatePacket statePacket = null;
            if (Time.unscaledTime >= nextStateSendAt)
            {
                statePacket = BuildLocalState();
                nextStateSendAt = Time.unscaledTime + Mathf.Max(0.05f, stateSendInterval);
            }

            SyncRequest reqPayload = new SyncRequest
            {
                action = "sync",
                roomId = string.IsNullOrWhiteSpace(roomId) ? "global_lobby" : roomId,
                clientId = clientId,
                clientName = string.IsNullOrWhiteSpace(clientName) ? "Player" : clientName,
                sinceSeq = Math.Max(0, lastSeq),
                requestSnapshot = !snapshotApplied,
                state = statePacket,
                ops = opsChunk
            };

            string json = JsonUtility.ToJson(reqPayload);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 10;
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    RequeueOps(opsChunk);
                    if (verboseLogs)
                        Debug.LogWarning("[LobbySync] HTTP error: " + req.error);
                    yield break;
                }

                string responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                SyncResponse response = null;
                try { response = JsonUtility.FromJson<SyncResponse>(responseText); }
                catch { }

                if (response == null || !response.ok)
                {
                    RequeueOps(opsChunk);
                    if (verboseLogs)
                        Debug.LogWarning("[LobbySync] Invalid response: " + responseText);
                    yield break;
                }

                lastSeq = Math.Max(lastSeq, response.seq);

                if (!snapshotApplied && response.snapshot != null)
                {
                    ApplySnapshot(response.snapshot);
                    snapshotApplied = true;
                }

                ApplyIncomingOps(response.ops);
                UpdateGhosts(response.players);
            }
        }

        private string ResolveEndpoint()
        {
            if (!string.IsNullOrWhiteSpace(endpointUrl))
            {
                cachedEndpoint = endpointUrl.Trim();
                return cachedEndpoint;
            }

            if (!string.IsNullOrWhiteSpace(cachedEndpoint))
                return cachedEndpoint;

            string fromPrefs = PlayerPrefs.GetString(EndpointPrefKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(fromPrefs))
            {
                cachedEndpoint = fromPrefs.Trim();
                return cachedEndpoint;
            }

            return string.Empty;
        }

        private void EnsureClientIdentity()
        {
            clientId = PlayerPrefs.GetString(ClientIdPrefKey, string.Empty);
            clientName = PlayerPrefs.GetString(ClientNamePrefKey, "Player");

            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = "guest_" + Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(ClientIdPrefKey, clientId);
                PlayerPrefs.Save();
            }
        }

        private void RequestIdentityFromWeb()
        {
            if (identityRequested)
                return;

            identityRequested = true;

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                LobbySync_RequestIdentity(gameObject.name, nameof(OnIdentityResolved));
            }
            catch
            {
                // Keep generated fallback identity.
            }
#endif
        }

        private Transform ResolveLocalPlayer()
        {
            if (localPlayer != null && localPlayer.gameObject.activeInHierarchy)
                return localPlayer;

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                localPlayer = tagged.transform;
                return localPlayer;
            }

            PlayerCharacterController controller = FindFirstObjectByType<PlayerCharacterController>();
            if (controller != null)
            {
                localPlayer = controller.transform;
                return localPlayer;
            }

            return null;
        }

        private PlayerStatePacket BuildLocalState()
        {
            Transform player = ResolveLocalPlayer();
            if (player == null)
                return null;

            return new PlayerStatePacket
            {
                clientId = clientId,
                name = string.IsNullOrWhiteSpace(clientName) ? "Player" : clientName,
                x = player.position.x,
                y = player.position.y,
                z = player.position.z,
                ry = player.eulerAngles.y,
                inLobby = wellGenerator == null || wellGenerator.IsInLobbyMode,
                t = EpochMs()
            };
        }

        private void EnqueueOp(OpPacket op)
        {
            if (op == null || string.IsNullOrWhiteSpace(op.opId))
                return;

            pendingOps.Add(op);
            if (pendingOps.Count > 1000)
                pendingOps.RemoveRange(0, pendingOps.Count - 1000);
        }

        private OpPacket[] DequeueOpsChunk()
        {
            if (pendingOps.Count == 0)
                return Array.Empty<OpPacket>();

            int take = Mathf.Clamp(maxOpsPerRequest, 1, 256);
            if (pendingOps.Count < take)
                take = pendingOps.Count;

            OpPacket[] chunk = pendingOps.GetRange(0, take).ToArray();
            pendingOps.RemoveRange(0, take);
            return chunk;
        }

        private void RequeueOps(OpPacket[] ops)
        {
            if (ops == null || ops.Length == 0)
                return;

            pendingOps.InsertRange(0, ops);
            if (pendingOps.Count > 1000)
                pendingOps.RemoveRange(1000, pendingOps.Count - 1000);
        }

        private void ApplySnapshot(SnapshotCell[] snapshot)
        {
            if (snapshot == null || snapshot.Length == 0)
                return;

            if (wellGenerator == null)
                return;

            VoxelIsland island = wellGenerator.ActiveIsland;
            if (island == null)
                return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                SnapshotCell c = snapshot[i];
                if (c == null || !island.InBounds(c.x, c.y, c.z))
                    continue;

                if (c.blockType <= 0)
                    island.RemoveVoxel(c.x, c.y, c.z, false);
                else
                    island.SetVoxel(c.x, c.y, c.z, (BlockType)Mathf.Clamp(c.blockType, 1, (int)BlockType.Grass), false);
            }

            island.RebuildMesh();

            if (verboseLogs)
                Debug.Log("[LobbySync] Snapshot applied: " + snapshot.Length + " cells");
        }

        private void ApplyIncomingOps(OpPacket[] ops)
        {
            if (ops == null || ops.Length == 0 || lobbyEditor == null)
                return;

            for (int i = 0; i < ops.Length; i++)
            {
                OpPacket op = ops[i];
                if (op == null || string.IsNullOrWhiteSpace(op.opId))
                    continue;

                // FIX #12: сначала фильтруем свои операции, потом добавляем в HashSet
                if (!string.IsNullOrWhiteSpace(op.from) && op.from == clientId)
                    continue;

                // FIX #4: ограничиваем размер seenOpIds — удаляем старые записи при превышении лимита
                if (!seenOpIds.Add(op.opId))
                    continue;
                seenOpIdsOrder.Enqueue(op.opId);
                while (seenOpIdsOrder.Count > MaxSeenOpIds)
                    seenOpIds.Remove(seenOpIdsOrder.Dequeue());

                Vector3Int pos = new Vector3Int(op.x, op.y, op.z);
                switch (op.kind)
                {
                    case "place":
                        lobbyEditor.ApplyNetworkPlaceBlock(pos, (BlockType)Mathf.Clamp(op.blockType, 1, (int)BlockType.Grass));
                        break;
                    case "remove":
                        lobbyEditor.ApplyNetworkRemoveBlock(pos);
                        break;
                }
            }
        }

        private void UpdateGhosts(PlayerStatePacket[] players)
        {
            if (players == null)
                return;

            bool localInLobby = wellGenerator == null || wellGenerator.IsInLobbyMode;
            float now = Time.unscaledTime;

            for (int i = 0; i < players.Length; i++)
            {
                PlayerStatePacket p = players[i];
                if (p == null || string.IsNullOrWhiteSpace(p.clientId))
                    continue;
                if (p.clientId == clientId)
                    continue;
                if (!p.inLobby || !localInLobby)
                    continue;

                GhostView ghost = GetOrCreateGhost(p.clientId);
                if (ghost == null || ghost.tr == null)
                    continue;

                Vector3 target = new Vector3(p.x, p.y, p.z);
                ghost.tr.position = Vector3.Lerp(ghost.tr.position, target, 0.75f);
                ghost.tr.rotation = Quaternion.Euler(0f, p.ry, 0f);
                ghost.lastSeenAt = now;

                if (ghost.label != null)
                {
                    ghost.label.text = string.IsNullOrWhiteSpace(p.name) ? "Player" : p.name;
                    Camera cam = Camera.main;
                    if (cam != null)
                        ghost.label.transform.rotation = Quaternion.LookRotation(ghost.label.transform.position - cam.transform.position);
                }
            }
        }

        private GhostView GetOrCreateGhost(string id)
        {
            if (ghosts.TryGetValue(id, out GhostView existing) && existing != null && existing.root != null)
                return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "SharedGhost_" + id;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            // FIX #5: используем один шаред материал для всех призраков — нет утечки памяти
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (_ghostSharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    _ghostSharedMaterial = new Material(shader);
                    _ghostSharedMaterial.color = new Color(0.25f, 0.95f, 0.85f, 0.45f);
                    _ghostSharedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _ghostSharedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _ghostSharedMaterial.SetInt("_ZWrite", 0);
                    _ghostSharedMaterial.renderQueue = 3000;
                }
                renderer.sharedMaterial = _ghostSharedMaterial;
            }

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            TextMesh label = labelGo.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.08f;
            label.fontSize = 42;
            label.color = Color.white;
            label.text = "Player";

            GhostView created = new GhostView
            {
                id = id,
                root = go,
                tr = go.transform,
                label = label,
                lastSeenAt = Time.unscaledTime
            };

            ghosts[id] = created;
            return created;
        }

        private void CleanupStaleGhosts()
        {
            if (ghosts.Count == 0)
                return;

            float ttl = Mathf.Max(2f, ghostTimeoutSeconds);
            float now = Time.unscaledTime;
            List<string> removeIds = null;

            foreach (KeyValuePair<string, GhostView> kv in ghosts)
            {
                GhostView g = kv.Value;
                // Remove both null/destroyed entries AND timed-out ones
                if (g == null || g.root == null || now - g.lastSeenAt > ttl)
                {
                    if (removeIds == null)
                        removeIds = new List<string>();
                    removeIds.Add(kv.Key);
                }
            }

            if (removeIds == null)
                return;

            for (int i = 0; i < removeIds.Count; i++)
            {
                string id = removeIds[i];
                if (ghosts.TryGetValue(id, out GhostView g))
                {
                    if (g != null && g.root != null)
                        Destroy(g.root);
                }
                ghosts.Remove(id);
            }
        }

        private void DestroyAllGhosts()
        {
            foreach (KeyValuePair<string, GhostView> kv in ghosts)
            {
                GhostView g = kv.Value;
                if (g != null && g.root != null)
                    Destroy(g.root);
            }
            ghosts.Clear();
        }

        private static string NewOpId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static long EpochMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
