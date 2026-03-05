using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using YG;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class YandexAsyncMultiplayerManager : MonoBehaviour
    {
        [Header("Sessions")]
        [Range(1, 10)] public int opponentsToLoad = 10;
        [Min(50)] public int maxOpponentTurnTimeMs = 250;
        [Min(0.05f)] public float commitInterval = 0.25f;
        [Min(5f)] public float autoPushIntervalSeconds = 25f;
        public bool verboseLogs = false; // FIX #13: в релизе должно быть false, иначе дебаг-GUI видят все игроки

        [Header("State Commit Filter")]
        [Min(0f)] public float minStateMoveDistance = 0.05f;
        [Min(0f)] public float minStateYawDelta = 2f;
        [Min(0.1f)] public float forceStateCommitInterval = 2f;

        private WellGenerator wellGenerator;
        private Transform localPlayer;

        private bool initRequested;
        private bool initialized;
        private float nextCommitTime;
        private float nextPushTime;
        private float nextForcedStateTime;
        private int totalIncomingTransactions;
        private bool hasLastState;
        private Vector3 lastStatePos;
        private float lastStateYaw;

        private readonly Dictionary<string, GhostAvatar> ghosts = new Dictionary<string, GhostAvatar>();
        private readonly Queue<string> pendingCommitPayloads = new Queue<string>();

        [Serializable]
        private class MetaRange
        {
            public int min;
            public int max;
        }

        [Serializable]
        private class InitMeta
        {
            public MetaRange meta1;
            public MetaRange meta2;
        }

        [Serializable]
        private class InitPayload
        {
            public int count;
            public bool isEventBased;
            public int maxOpponentTurnTime;
            public InitMeta meta;
        }

        [Serializable]
        private class InitResponse
        {
            public bool ok;
            public int opponentsCount;
            public string reason;
        }

        [Serializable]
        private class TxBatch
        {
            public string opponentId;
            public TxItem[] transactions;
        }

        [Serializable]
        private class TxItem
        {
            public string id;
            public float time;
            public string payloadJson;
        }

        [Serializable]
        private class FinishPayload
        {
            public string opponentId;
        }

        [Serializable]
        private class CommitPayload
        {
            public string kind; // "state" or "event"
            public string eventType;
            public string playerId;
            public string playerName;
            public bool isGuest;

            public float x;
            public float y;
            public float z;
            public float ry;
            public bool inLobby;

            public int gx;
            public int gy;
            public int gz;
            public int moneyDelta;
            public int xpDelta;
            public int miningLevel;
            public int blockType;
            public int mineDepth;
            public string mineName;
        }

        [Serializable]
        private class IdentityPayload
        {
            public string playerId;
            public string playerName;
        }

        [Serializable]
        private class PushMetaPayload
        {
            public int meta1;
            public int meta2;
            public int meta3;
        }

        private class GhostAvatar
        {
            public string id;
            public GameObject gameObject;
            public Transform transform;
            public TextMesh label;
            public bool inLobby = true;
            public string playerId;
            public string playerName;
            public bool isGuest = true;

            public int money;
            public int xp;
            public int miningLevel = 1;
            public int minedBlocks;
            public string lastEvent = "state";
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int YandexMP_IsAvailable();

        [DllImport("__Internal")]
        private static extern void YandexMP_Init(string gameObjectName, string initMethod, string txMethod, string finishMethod, string configJson);

        [DllImport("__Internal")]
        private static extern void YandexMP_Commit(string payloadJson);

        [DllImport("__Internal")]
        private static extern void YandexMP_Push(string metaJson);

        [DllImport("__Internal")]
        private static extern void LobbySync_RequestIdentity(string gameObjectName, string callbackMethod);
#endif

        private const string AsyncPlayerIdPrefKey = "svs_async_player_id";
        private const string AsyncPlayerNamePrefKey = "svs_async_player_name";

        private string localPlayerId = string.Empty;
        private string localPlayerName = "Player";
        private bool localIsGuest = true;
        private bool identityRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<YandexAsyncMultiplayerManager>() != null)
                return;

            GameObject go = new GameObject("YandexAsyncMultiplayerManager");
            DontDestroyOnLoad(go);
            go.AddComponent<YandexAsyncMultiplayerManager>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += OnSdkReady;
            AsyncGameplayEvents.OnEvent += OnLocalGameplayEvent;
            EnsureLocalIdentity();
            RequestIdentityFromWeb();
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= OnSdkReady;
            AsyncGameplayEvents.OnEvent -= OnLocalGameplayEvent;
        }

        private IEnumerator Start()
        {
            while (wellGenerator == null)
            {
                wellGenerator = FindFirstObjectByType<WellGenerator>();
                if (wellGenerator == null)
                    yield return null;
            }

            TryInit();
        }

        private void Update()
        {
            if (!initialized)
            {
                TryInit();
                return;
            }

            if (Time.unscaledTime >= nextCommitTime)
            {
                CommitLocalState();
                nextCommitTime = Time.unscaledTime + commitInterval;
            }

            if (Time.unscaledTime >= nextPushTime)
            {
                PushSession();
                nextPushTime = Time.unscaledTime + Mathf.Max(5f, autoPushIntervalSeconds);
            }
        }

        private void OnGUI()
        {
            if (!verboseLogs)
                return;

            string status = initialized ? "ON" : (initRequested ? "INIT" : "OFF");
            GUI.Label(new Rect(10, 88, 360, 20), $"AsyncMP: {status} ghosts={ghosts.Count} tx={totalIncomingTransactions}");
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                PushSession();
        }

        private void OnApplicationQuit()
        {
            PushSession();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                PushSession();
        }

        private void OnSdkReady()
        {
            TryInit();
        }

        private void TryInit()
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            return;
#else
            if (initialized || initRequested)
                return;

            if (!YG2.isSDKEnabled)
                return;

            if (YandexMP_IsAvailable() != 1)
                return;

            InitPayload init = new InitPayload
            {
                count = Mathf.Clamp(opponentsToLoad, 1, 10),
                isEventBased = true,
                maxOpponentTurnTime = Mathf.Max(50, maxOpponentTurnTimeMs),
                meta = new InitMeta
                {
                    meta1 = new MetaRange { min = 0, max = Mathf.Max(10000, GlobalEconomy.Money + 100000) },
                    meta2 = new MetaRange { min = Mathf.Max(1, GlobalEconomy.MiningLevel - 5), max = GlobalEconomy.MiningLevel + 5 }
                }
            };

            string configJson = JsonUtility.ToJson(init);
            initRequested = true;
            YandexMP_Init(gameObject.name, nameof(OnMpInitResult), nameof(OnOpponentTransactions), nameof(OnOpponentFinish), configJson);
#endif
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

            if (!string.IsNullOrWhiteSpace(payload.playerId))
                localPlayerId = payload.playerId.Trim();
            if (!string.IsNullOrWhiteSpace(payload.playerName))
                localPlayerName = payload.playerName.Trim();

            localIsGuest = IsGuestIdentity(localPlayerId, localPlayerName);
            SaveLocalIdentityPrefs();
        }

        public void OnMpInitResult(string json)
        {
            initRequested = false;

            InitResponse response = null;
            try { response = JsonUtility.FromJson<InitResponse>(json); }
            catch { }

            if (response != null && response.ok)
            {
                initialized = true;
                nextCommitTime = Time.unscaledTime + commitInterval;
                nextPushTime = Time.unscaledTime + Mathf.Max(5f, autoPushIntervalSeconds);
                nextForcedStateTime = Time.unscaledTime + Mathf.Max(0.1f, forceStateCommitInterval);
                FlushPendingCommits();
                if (verboseLogs)
                    Debug.Log($"[YandexAsyncMP] Init OK. Opponents loaded: {response.opponentsCount}");
                return;
            }

            if (verboseLogs)
                Debug.LogWarning($"[YandexAsyncMP] Init failed: {response?.reason ?? "unknown"}");
        }

        public void OnOpponentTransactions(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            TxBatch batch = null;
            try { batch = JsonUtility.FromJson<TxBatch>(json); }
            catch { }

            if (batch == null || string.IsNullOrWhiteSpace(batch.opponentId) || batch.transactions == null)
                return;

            GhostAvatar ghost = GetOrCreateGhost(batch.opponentId);
            for (int i = 0; i < batch.transactions.Length; i++)
            {
                TxItem tx = batch.transactions[i];
                if (tx == null || string.IsNullOrWhiteSpace(tx.payloadJson))
                    continue;

                CommitPayload payload = null;
                try { payload = JsonUtility.FromJson<CommitPayload>(tx.payloadJson); }
                catch { }

                if (payload == null || string.IsNullOrWhiteSpace(payload.kind))
                    continue;

                if (payload.kind == "state")
                    ApplyGhostState(ghost, payload);
                else if (payload.kind == "event")
                    ApplyGhostEvent(ghost, payload);

                totalIncomingTransactions++;
            }

            UpdateGhostLabel(ghost);
        }

        public void OnOpponentFinish(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            FinishPayload payload = null;
            try { payload = JsonUtility.FromJson<FinishPayload>(json); }
            catch { }

            if (payload == null || string.IsNullOrWhiteSpace(payload.opponentId))
                return;

            if (!ghosts.TryGetValue(payload.opponentId, out GhostAvatar ghost) || ghost?.gameObject == null)
                return;

            ghost.gameObject.SetActive(false);
        }

        private void CommitLocalState()
        {
            Transform player = ResolveLocalPlayer();
            if (player == null)
                return;

            Vector3 pos = player.position;
            float yaw = player.eulerAngles.y;
            float forceAfter = Mathf.Max(0.1f, forceStateCommitInterval);

            if (hasLastState)
            {
                float moveThresholdSqr = Mathf.Max(0f, minStateMoveDistance) * Mathf.Max(0f, minStateMoveDistance);
                float movedSqr = (pos - lastStatePos).sqrMagnitude;
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, lastStateYaw));
                bool changed = movedSqr >= moveThresholdSqr || yawDelta >= Mathf.Max(0f, minStateYawDelta);
                bool forced = Time.unscaledTime >= nextForcedStateTime;

                if (!changed && !forced)
                    return;
            }

            bool inLobby = wellGenerator == null || wellGenerator.IsInLobbyMode;
            CommitPayload payload = new CommitPayload
            {
                kind = "state",
                playerId = localPlayerId,
                playerName = localPlayerName,
                isGuest = localIsGuest,
                x = pos.x,
                y = pos.y,
                z = pos.z,
                ry = yaw,
                inLobby = inLobby,
                miningLevel = GlobalEconomy.MiningLevel
            };

            CommitPayloadJson(JsonUtility.ToJson(payload));
            hasLastState = true;
            lastStatePos = pos;
            lastStateYaw = yaw;
            nextForcedStateTime = Time.unscaledTime + forceAfter;
        }

        private void OnLocalGameplayEvent(AsyncGameplayEvent e)
        {
            CommitPayload payload = new CommitPayload
            {
                kind = "event",
                eventType = e.Type.ToString(),
                playerId = localPlayerId,
                playerName = localPlayerName,
                isGuest = localIsGuest,
                gx = e.gx,
                gy = e.gy,
                gz = e.gz,
                moneyDelta = e.moneyDelta,
                xpDelta = e.xpDelta,
                miningLevel = e.miningLevel,
                inLobby = e.inLobby,
                blockType = e.blockType,
                mineDepth = e.mineDepth,
                mineName = e.mineName ?? string.Empty
            };

            CommitPayloadJson(JsonUtility.ToJson(payload));

            if (e.Type == AsyncGameplayEventType.WorldSwitch)
            {
                PushSession();
                nextPushTime = Time.unscaledTime + Mathf.Max(5f, autoPushIntervalSeconds);
            }
        }

        private void CommitPayloadJson(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
                return;

            if (!initialized)
            {
                if (pendingCommitPayloads.Count > 128)
                    pendingCommitPayloads.Dequeue();
                pendingCommitPayloads.Enqueue(payloadJson);
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            YandexMP_Commit(payloadJson);
#endif
        }

        private void FlushPendingCommits()
        {
            if (!initialized)
                return;

            while (pendingCommitPayloads.Count > 0)
            {
                string payloadJson = pendingCommitPayloads.Dequeue();
#if UNITY_WEBGL && !UNITY_EDITOR
                YandexMP_Commit(payloadJson);
#endif
            }
        }

        private void PushSession()
        {
            if (!initialized)
                return;

            PushMetaPayload meta = new PushMetaPayload
            {
                meta1 = Mathf.Max(0, GlobalEconomy.Money),
                meta2 = Mathf.Max(1, GlobalEconomy.MiningLevel),
                meta3 = Mathf.Max(0, GlobalEconomy.MiningXP)
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            YandexMP_Push(JsonUtility.ToJson(meta));
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

        private GhostAvatar GetOrCreateGhost(string opponentId)
        {
            if (ghosts.TryGetValue(opponentId, out GhostAvatar existing) && existing != null && existing.gameObject != null)
                return existing;

            GameObject go = new GameObject();
            go.name = "AsyncGhost_" + opponentId;
            var mix = go.AddComponent<BlockyMixCharacter>();
            mix.rebuildOnAwake = false;
            mix.hideBaseRenderer = false;
            mix.autoAddAnimator = false;
            mix.overallScale = 1.0f;
            mix.visualYOffset = 0.02f;
            mix.shirtColor = new Color(0.12f, 0.62f, 0.90f, 1f);
            mix.pantsColor = new Color(0.10f, 0.28f, 0.45f, 1f);
            mix.accentColor = new Color(0.55f, 0.92f, 1f, 1f);
            mix.bootColor = new Color(0.08f, 0.18f, 0.28f, 1f);
            mix.gloveColor = new Color(0.18f, 0.50f, 0.68f, 1f);
            mix.stripeColor = new Color(0.82f, 0.97f, 1f, 1f);
            mix.Rebuild();
            SetGhostVisualStyle(go, new Color(0.2f, 0.9f, 1f, 0.45f));

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            TextMesh label = labelGo.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.08f;
            label.fontSize = 42;
            label.color = Color.white;
            label.text = "ghost";

            GhostAvatar created = new GhostAvatar
            {
                id = opponentId,
                gameObject = go,
                transform = go.transform,
                label = label
            };

            ghosts[opponentId] = created;
            return created;
        }

        // FIX #5: статический shared material для призраков AsyncMP — не создаём новый на каждый рендерер
        private static Material _asyncGhostSharedMaterial;

        private static void SetGhostVisualStyle(GameObject root, Color tint)
        {
            if (root == null)
                return;

            if (_asyncGhostSharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _asyncGhostSharedMaterial = new Material(shader);
                _asyncGhostSharedMaterial.color = tint;
                _asyncGhostSharedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _asyncGhostSharedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _asyncGhostSharedMaterial.SetInt("_ZWrite", 0);
                _asyncGhostSharedMaterial.renderQueue = 3000;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null)
                    continue;
                mr.sharedMaterial = _asyncGhostSharedMaterial;
            }
        }

        private void ApplyGhostState(GhostAvatar ghost, CommitPayload payload)
        {
            if (ghost == null || ghost.transform == null)
                return;

            ApplyGhostIdentityFromPayload(ghost, payload);
            ghost.inLobby = payload.inLobby;
            ghost.miningLevel = Mathf.Max(1, payload.miningLevel);
            if (string.IsNullOrWhiteSpace(ghost.lastEvent))
                ghost.lastEvent = "state";
            bool localInLobby = wellGenerator == null || wellGenerator.IsInLobbyMode;
            bool shouldShow = localInLobby && ghost.inLobby;
            if (ghost.gameObject.activeSelf != shouldShow)
                ghost.gameObject.SetActive(shouldShow);

            if (!shouldShow)
                return;

            Vector3 targetPos = new Vector3(payload.x, payload.y, payload.z);
            ghost.transform.position = Vector3.Lerp(ghost.transform.position, targetPos, 0.6f);
            ghost.transform.rotation = Quaternion.Euler(0f, payload.ry, 0f);
        }

        private void ApplyGhostEvent(GhostAvatar ghost, CommitPayload payload)
        {
            if (ghost == null)
                return;

            ApplyGhostIdentityFromPayload(ghost, payload);
            ghost.lastEvent = payload.eventType;
            ghost.miningLevel = Mathf.Max(1, payload.miningLevel);

            switch (payload.eventType)
            {
                case nameof(AsyncGameplayEventType.MineBlock):
                    ghost.minedBlocks++;
                    ghost.xp += Mathf.Max(0, payload.xpDelta);
                    break;
                case nameof(AsyncGameplayEventType.SellBackpack):
                case nameof(AsyncGameplayEventType.SellMine):
                    ghost.money += payload.moneyDelta;
                    break;
                case nameof(AsyncGameplayEventType.BuyMine):
                    ghost.money += payload.moneyDelta;
                    break;
                case nameof(AsyncGameplayEventType.WorldSwitch):
                    ghost.inLobby = payload.inLobby;
                    break;
            }
        }

        private void UpdateGhostLabel(GhostAvatar ghost)
        {
            if (ghost?.label == null)
                return;

            string header = BuildGhostHeader(ghost);
            ghost.label.text =
                $"{header}\n" +
                $"Lv {ghost.miningLevel}\n" +
                $"Mined {ghost.minedBlocks}\n" +
                $"$ {ghost.money}\n" +
                $"{ghost.lastEvent}";

            Camera cam = Camera.main;
            if (cam != null)
                ghost.label.transform.rotation = Quaternion.LookRotation(ghost.label.transform.position - cam.transform.position);
        }

        private void ApplyGhostIdentityFromPayload(GhostAvatar ghost, CommitPayload payload)
        {
            if (ghost == null || payload == null)
                return;

            if (!string.IsNullOrWhiteSpace(payload.playerId))
                ghost.playerId = payload.playerId.Trim();
            if (!string.IsNullOrWhiteSpace(payload.playerName))
                ghost.playerName = payload.playerName.Trim();

            string identityId = !string.IsNullOrWhiteSpace(ghost.playerId) ? ghost.playerId : ghost.id;
            string identityName = ghost.playerName;
            ghost.isGuest = payload.isGuest || IsGuestIdentity(identityId, identityName);
        }

        private string BuildGhostHeader(GhostAvatar ghost)
        {
            if (ghost == null)
                return "Player";

            string id = !string.IsNullOrWhiteSpace(ghost.playerId) ? ghost.playerId : ghost.id;
            string shortId = ShortId(id);

            if (!ghost.isGuest && !string.IsNullOrWhiteSpace(ghost.playerName) && !string.Equals(ghost.playerName, "Player", StringComparison.OrdinalIgnoreCase))
                return $"{ghost.playerName} [{shortId}]";

            return $"New player [{shortId}]";
        }

        private static bool IsGuestIdentity(string playerId, string playerName)
        {
            bool idLooksGuest = !string.IsNullOrWhiteSpace(playerId) &&
                                playerId.StartsWith("guest_", StringComparison.OrdinalIgnoreCase);
            bool nameDefault = string.IsNullOrWhiteSpace(playerName) ||
                               string.Equals(playerName, "Player", StringComparison.OrdinalIgnoreCase);
            return idLooksGuest || nameDefault;
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "unknown";
            id = id.Trim();
            if (id.Length <= 10)
                return id;
            return id.Substring(0, 4) + "..." + id.Substring(id.Length - 4);
        }

        private void EnsureLocalIdentity()
        {
            localPlayerId = PlayerPrefs.GetString(AsyncPlayerIdPrefKey, string.Empty);
            localPlayerName = PlayerPrefs.GetString(AsyncPlayerNamePrefKey, "Player");

            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                localPlayerId = "guest_" + Guid.NewGuid().ToString("N");
                localPlayerName = "Player";
                SaveLocalIdentityPrefs();
            }

            localIsGuest = IsGuestIdentity(localPlayerId, localPlayerName);
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
                // fallback identity from prefs/guest remains active
            }
#endif
        }

        private void SaveLocalIdentityPrefs()
        {
            if (!string.IsNullOrWhiteSpace(localPlayerId))
                PlayerPrefs.SetString(AsyncPlayerIdPrefKey, localPlayerId);
            if (!string.IsNullOrWhiteSpace(localPlayerName))
                PlayerPrefs.SetString(AsyncPlayerNamePrefKey, localPlayerName);
            PlayerPrefs.Save();
        }
    }
}
