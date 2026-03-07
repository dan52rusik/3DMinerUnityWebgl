using System;
using System.Collections.Generic;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Island generator. Fills VoxelIsland with data without separate block objects.
    /// Supports two independent worlds: Lobby and Private Island.
    /// </summary>
        [RequireComponent(typeof(VoxelIsland))]
    public class WellGenerator : MonoBehaviour
    {
        [Header("Well Size")]
        public int wellWidth = 5;
        public int wellLength = 5;
        public int wellDepth = 10;

        [Header("Ground Padding around the Well")]
        public int padding = 5;

        [Header("Lobby (spawn zone)")] 
        [Tooltip("Width (X) of flat lobby platform in blocks.")]
        public int lobbyWidth  = 50;
        [Tooltip("Length (Z) of flat lobby platform in blocks.")]
        public int lobbyLength = 50;
        [Tooltip("How many layers can be built ABOVE the floor. Floor is at gridY=lobbyBuildAbove, above is gridY=0..lobbyBuildAbove-1.")]
        public int lobbyBuildAbove = 8;

        [Header("Lobby Streaming")]
        [Tooltip("Lobby always stays in the scene. When switching to the island, it is only visually hidden by distance.")]
        public bool keepLobbyAlwaysLoaded = true;
        [Tooltip("If player is further than this distance from lobby center, lobby can stop rendering.")]
        public float lobbyRenderDistance = 100f;
        [Tooltip("Whether to disable lobby collider when lobby is hidden by distance.")]
        public bool disableLobbyColliderWhenFar = true;

        [Header("Player Spawn")]
        public GameObject playerPrefab;
        public Transform playerToPlace;
        public float playerSpawnHeight = 1.05f;
        public float safeSpawnDropOffset = 2.5f;
        [Tooltip("Don't teleport player to the center of the mine when placing it.")]
        public bool keepPlayerPositionWhenPlacingMine = true;
        [Tooltip("When returning from lobby to island, return player to their last position on the island.")]
        public bool returnToLastIslandPosition = true;

        [Header("Fall Recovery")]
        [Tooltip("If player falls too low, return them to a safe point of the current world.")]
        public bool respawnOnFall = true;
        [Min(4f)] public float fallRespawnExtraDepth = 60f;
        [Min(0.1f)] public float fallRespawnCooldown = 1.0f;

        [Header("Private Island Offset")]
        public Vector3 privateIslandOffset = new Vector3(500f, 0f, 500f);

        // ─── Mining Manager (Dependency) ────────────────────────────────────
        public MiningManager Mining => miningManager;
        private MiningManager miningManager;

        // ─── Public API (Legacy Proxies for Compatibility) ──────────────────
        public bool IsMineGenerated => miningManager != null && miningManager.IsMineGenerated;
        public MineInstance ActiveMine => miningManager?.ActiveMine;
        public IReadOnlyList<MineInstance> PlacedMines => miningManager?.PlacedMines;
        public List<BlockData> blockDataConfig => miningManager?.blockDataConfig;
        
        // ─── World State ────────────────────────────────────────────────────
        public int LobbyFloorY => lobbyBuildAbove;
        public bool IsInLobbyMode { get; private set; } = true;
        public VoxelIsland ActiveIsland => IsInLobbyMode ? lobbyIsland : (playerIsland ?? lobbyIsland);
        public VoxelIsland PrivateIsland => playerIsland;
        public bool IsIslandGenerated => playerIsland != null;

        public event System.Action OnFlatPlotReady;
        public event Action<bool> OnWorldSwitch; // true=lobby, false=mine

        // ─── Internal State ──────────────────────────────────────────────────
        private VoxelIsland lobbyIsland;
        private VoxelIsland playerIsland;
        private MeshRenderer lobbyRenderer;
        private MeshCollider lobbyCollider;
        private float nextFallRespawnAt;

        private Vector3 lobbySpawnPos;
        private Vector3 islandSpawnPos;
        private bool hasIslandSpawnPos;
        private Vector3 customIslandSpawnPos;
        private bool hasCustomIslandSpawnPos;

        void Awake()
        {
            lobbyIsland = GetComponent<VoxelIsland>();
            if (lobbyIsland == null) lobbyIsland = GetComponentInChildren<VoxelIsland>();
            if (lobbyIsland == null) lobbyIsland = gameObject.AddComponent<VoxelIsland>();
            
            lobbyRenderer = lobbyIsland.GetComponent<MeshRenderer>();
            lobbyCollider = lobbyIsland.GetComponent<MeshCollider>();

            miningManager = GetComponent<MiningManager>() ?? gameObject.AddComponent<MiningManager>();
            miningManager.Initialize(this);

            if (GetComponent<PickaxeShopUI>() == null) gameObject.AddComponent<PickaxeShopUI>();
            if (GetComponent<MineMarket>() == null) gameObject.AddComponent<MineMarket>();

            Transform p = ResolveOrSpawnPlayer();
            lobbySpawnPos = p != null ? p.position : Vector3.zero;
        }

        void Start()
        {
            IsInLobbyMode = true;
            GenerateFlatPlot();
        }

        void Update()
        {
            UpdateLobbyStreamingVisibility();
            TryRecoverFromFall();
        }

        private void SyncColorsToActiveIsland()
        {
            if (ActiveIsland == null || miningManager == null) return;
            
            int typeCount = System.Enum.GetValues(typeof(BlockType)).Length;
            Color[] cols = new Color[typeCount];
            
            for (int i = 0; i < typeCount && i < ActiveIsland.blockColors.Length; i++)
                cols[i] = ActiveIsland.blockColors[i];

            if (miningManager.blockDataConfig != null)
            {
                foreach (BlockData bd in miningManager.blockDataConfig)
                {
                    int idx = (int)bd.type;
                    if (idx >= 0 && idx < typeCount && bd.blockColor.a > 0.01f) 
                        cols[idx] = bd.blockColor;
                }
            }
            ActiveIsland.blockColors = cols;
        }

        public void GenerateFlatPlot()
        {
            if (IsInLobbyMode)
            {
                lobbyIsland.gameObject.SetActive(true);
                if (playerIsland != null) playerIsland.gameObject.SetActive(false);
                Physics.SyncTransforms();

                int lw = Mathf.Max(32, lobbyWidth);
                int ll = Mathf.Max(32, lobbyLength);
                lobbyIsland.Init(lw, lobbyBuildAbove + 1, ll, 0, 0);

                int floorY = lobbyBuildAbove;
                for (int x = 0; x < lobbyIsland.TotalX; x++)
                for (int z = 0; z < lobbyIsland.TotalZ; z++)
                    lobbyIsland.SetVoxel(x, floorY, z, BlockType.Dirt);

                SyncColorsToActiveIsland();
                lobbyIsland.RebuildMesh();
                
                lobbySpawnPos = new Vector3(lw / 2f, -floorY, ll / 2f);
                SpawnPlayerAt(lobbySpawnPos);
                OnFlatPlotReady?.Invoke(); 
            }
            else
            {
                if (keepLobbyAlwaysLoaded) lobbyIsland.gameObject.SetActive(true);
                else lobbyIsland.gameObject.SetActive(false);
                
                if (playerIsland == null) CreatePlayerIsland();
                playerIsland.gameObject.SetActive(true);
                
                Physics.SyncTransforms();
                SyncColorsToActiveIsland();
                
                miningManager.ApplyMinesToIsland();
            }
            UpdateLobbyStreamingVisibility();
        }

        private void CreatePlayerIsland()
        {
            GameObject go = new GameObject("PlayerPrivateIsland");
            go.transform.SetParent(this.transform.parent);
            go.transform.position = privateIslandOffset;

            playerIsland = go.AddComponent<VoxelIsland>();
            playerIsland.blockColors = (Color[])lobbyIsland.blockColors.Clone();
            
            MeshRenderer lobbyRen = lobbyIsland.GetComponent<MeshRenderer>();
            MeshRenderer playerRen = go.GetComponent<MeshRenderer>();
            if (lobbyRen != null && playerRen != null)
                playerRen.material = lobbyRen.material;

            int lw = Mathf.Max(64, lobbyWidth);
            int ll = Mathf.Max(64, lobbyLength);
            int totalHeight = lobbyBuildAbove + 32; 

            playerIsland.Init(lw, totalHeight, ll, 0, 0);

            int floorY = lobbyBuildAbove;
            float centerX = lw / 2f;
            float centerZ = ll / 2f;
            float radius = Mathf.Min(lw, ll) / 2.2f;

            for (int x = 0; x < playerIsland.TotalX; x++)
            for (int z = 0; z < playerIsland.TotalZ; z++)
            {
                float dx = x - centerX;
                float dz = z - centerZ;
                if (dx*dx + dz*dz > radius*radius) continue;

                playerIsland.SetVoxel(x, floorY, z, BlockType.Grass);
                for (int y = floorY + 1; y < playerIsland.TotalY; y++)
                    playerIsland.SetVoxel(x, y, z, BlockType.Dirt);
            }
            playerIsland.RebuildMesh();
            islandSpawnPos = playerIsland.transform.TransformPoint(new Vector3(lw / 2f, -LobbyFloorY, ll / 2f));
            hasIslandSpawnPos = true;
        }

        public void GeneratePlotExtension(int offsetX, int offsetZ, int width, int length)
        {
            Debug.Log($"[WellGenerator] Purchased plot +[{offsetX},{offsetZ}] size {width}x{length}");
        }

        // ─── Mining Proxy API ───────────────────────────────────────────────
        public void MineVoxel(int gx, int gy, int gz) => miningManager.MineVoxel(gx, gy, gz);
        public void GenerateMine(MineInstance mine) => miningManager.GenerateMine(mine);
        public void GenerateMineAt(MineInstance mine, int gx, int gz) => miningManager.GenerateMineAt(mine, gx, gz);
        public void DemolishMine() => miningManager.DemolishMine();
        public bool CanMineVoxel(int gx, int gy, int gz) => miningManager.CanMineVoxel(gx, gy, gz);
        public bool IsInsideWellArea(int gx, int gz) => miningManager.IsInsideWellArea(gx, gz);
        public bool IsWellLayerCleared(int depthGridY) => miningManager.IsWellLayerCleared(depthGridY);
        public int GetContiguousClearedDepth() => miningManager.GetContiguousClearedDepth();
        public int GetContiguousClearedDepthAtShaft(int sx, int sz) => miningManager.GetContiguousClearedDepthAtShaft(sx, sz);
        public void RestoreMineFromSave(MineInstance mine) => miningManager.RestoreMineFromSave(mine);
        public void RestoreMinesFromSave(List<MineInstance> mines) => miningManager.RestoreMinesFromSave(mines);

        public void NotifyMinePlaced(MineInstance mine)
        {
            IsInLobbyMode = false;
            OnWorldSwitch?.Invoke(false);
            AsyncGameplayEvents.PublishWorldSwitch(false);
            Debug.Log($"[WellGenerator] World switched to Mine: {mine.shopData.displayName}.");
        }

        public void SwitchToMine()
        {
            if (!IsInLobbyMode) return;
            IsInLobbyMode = false;
            
            if (playerIsland == null)
            {
                float rx = UnityEngine.Random.Range(500f, 2000f) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
                float rz = UnityEngine.Random.Range(500f, 2000f) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
                privateIslandOffset = new Vector3(rx, 0, rz);
                CreatePlayerIsland();
            }

            SetIslandActive(lobbyIsland, keepLobbyAlwaysLoaded);
            SetIslandActive(playerIsland, true);
            Physics.SyncTransforms();

            miningManager.ApplyMinesToIsland();
            SpawnPlayerAt(ResolvePreferredIslandSpawnPoint());

            OnWorldSwitch?.Invoke(false);
            AsyncGameplayEvents.PublishWorldSwitch(false);
            UpdateLobbyStreamingVisibility();
        }

        public void SwitchToLobby()
        {
            if (IsInLobbyMode) return;
            IsInLobbyMode = true;

            RememberCurrentIslandSpawnPoint();
            SetIslandActive(playerIsland, false);
            SetIslandActive(lobbyIsland, true);
            Physics.SyncTransforms();

            float cx = lobbyWidth / 2f;
            float cz = lobbyLength / 2f;
            SpawnPlayerAt(new Vector3(cx, -LobbyFloorY, cz));

            OnWorldSwitch?.Invoke(true);
            AsyncGameplayEvents.PublishWorldSwitch(true);
            UpdateLobbyStreamingVisibility();
        }

        private void SetIslandActive(VoxelIsland island, bool active)
        {
            if (island == null) return;
            if (island.gameObject == this.gameObject)
            {
                if (island.TryGetComponent(out MeshRenderer r)) r.enabled = active;
                if (island.TryGetComponent(out MeshCollider c)) c.enabled = active;
            }
            else island.gameObject.SetActive(active);
        }

        public void EnsurePrivateIslandAtOffset(Vector3 offset)
        {
            privateIslandOffset = offset;
            if (playerIsland == null)
            {
                CreatePlayerIsland();
            }
            else
            {
                playerIsland.transform.position = privateIslandOffset;
                islandSpawnPos = playerIsland.transform.TransformPoint(new Vector3(playerIsland.TotalX / 2f, -LobbyFloorY, playerIsland.TotalZ / 2f));
                hasIslandSpawnPos = true;
                Physics.SyncTransforms();
            }

            SetIslandActive(playerIsland, !IsInLobbyMode);
            SetIslandActive(lobbyIsland, true);
            UpdateLobbyStreamingVisibility();
        }

        public void ResetPlayerWorldForNewProgress()
        {
            OnboardingTutorial.ResetTutorialStatic();

            if (!IsInLobbyMode) SwitchToLobby();
            miningManager.ClearMines();

            foreach (SimpleElevator elev in GetComponentsInChildren<SimpleElevator>())
            {
                if (elev == null) continue;
                if (Application.isPlaying) Destroy(elev.gameObject);
                else DestroyImmediate(elev.gameObject);
            }

            if (playerIsland != null)
            {
                if (Application.isPlaying) Destroy(playerIsland.gameObject);
                else DestroyImmediate(playerIsland.gameObject);
                playerIsland = null;
            }

            hasIslandSpawnPos = false;
            islandSpawnPos = Vector3.zero;
            hasCustomIslandSpawnPos = false;
            customIslandSpawnPos = Vector3.zero;
            IsInLobbyMode = true;

            if (lobbyIsland != null) SetIslandActive(lobbyIsland, true);
            UpdateLobbyStreamingVisibility();
        }

        public void RememberCurrentIslandSpawnPoint()
        {
            if (playerIsland == null) return;
            Transform player = ResolveOrSpawnPlayer();
            if (player == null) return;

            Vector3 local = playerIsland.transform.InverseTransformPoint(player.position);
            if (local.x < 0f || local.z < 0f || local.x > playerIsland.TotalX || local.z > playerIsland.TotalZ)
                return;

            islandSpawnPos = player.position;
            hasIslandSpawnPos = true;
        }

        public bool SaveCurrentIslandSpawnPoint()
        {
            if (IsInLobbyMode || playerIsland == null) return false;
            Transform player = ResolveOrSpawnPlayer();
            if (player == null) return false;
            if (!TryGetSurfaceYAtOnActiveIsland(player.position.x, player.position.z, out float groundY)) return false;

            customIslandSpawnPos = new Vector3(player.position.x, groundY, player.position.z);
            hasCustomIslandSpawnPos = true;
            return true;
        }

        public bool HasCustomIslandSpawnPoint => hasCustomIslandSpawnPos;
        public Vector3 GetCustomIslandSpawnPoint() => customIslandSpawnPos;
        public void SetCustomIslandSpawnPointFromSave(Vector3 worldPos) { customIslandSpawnPos = worldPos; hasCustomIslandSpawnPos = true; }
        public void ClearCustomIslandSpawnPoint() { hasCustomIslandSpawnPos = false; customIslandSpawnPos = Vector3.zero; }

        private void UpdateLobbyStreamingVisibility()
        {
            if (lobbyIsland == null || lobbyRenderer == null) return;
            bool shouldShow = true;
            if (keepLobbyAlwaysLoaded && !IsInLobbyMode)
            {
                Transform player = ResolveOrSpawnPlayer();
                if (player != null)
                {
                    Vector3 lobbyCenter = lobbyIsland.transform.TransformPoint(new Vector3(lobbyWidth * 0.5f, -LobbyFloorY, lobbyLength * 0.5f));
                    float maxDist = Mathf.Max(1f, lobbyRenderDistance);
                    shouldShow = (player.position - lobbyCenter).sqrMagnitude <= maxDist * maxDist;
                }
            }
            if (!keepLobbyAlwaysLoaded) shouldShow = IsInLobbyMode;
            lobbyRenderer.enabled = shouldShow;
            if (lobbyCollider != null) lobbyCollider.enabled = IsInLobbyMode || shouldShow || !disableLobbyColliderWhenFar;
        }

        private void TryRecoverFromFall()
        {
            if (!respawnOnFall || Time.unscaledTime < nextFallRespawnAt) return;
            Transform player = ResolveOrSpawnPlayer();
            if (player == null || ActiveIsland == null) return;
            if (player.position.y >= GetMinAllowedPlayerY()) return;

            RespawnPlayerInCurrentWorld();
            nextFallRespawnAt = Time.unscaledTime + Mathf.Max(0.1f, fallRespawnCooldown);
        }

        private float GetMinAllowedPlayerY()
        {
            float extra = Mathf.Max(4f, fallRespawnExtraDepth);
            if (ActiveIsland == null) return -100f;

            // If we are on the island, we allow digging to the very bottom of the island (TotalY) + buffer.
            // On the island TotalY = 40 (8 build + 32 digging), so the bottom is at -40.
            if (!IsInLobbyMode)
            {
                return ActiveIsland.transform.position.y - ActiveIsland.TotalY - extra;
            }

            // In the lobby, we limit by the floor level.
            float lobbyFloorYWorld = (lobbyIsland != null) ? lobbyIsland.transform.position.y - LobbyFloorY : -LobbyFloorY;
            return lobbyFloorYWorld - extra;
        }

        private void RespawnPlayerInCurrentWorld()
        {
            if (IsInLobbyMode) SpawnPlayerAt(new Vector3(lobbyWidth / 2f, -LobbyFloorY, lobbyLength / 2f));
            else SpawnPlayerAt(ResolvePreferredIslandSpawnPoint());
        }

        private Vector3 ResolvePreferredIslandSpawnPoint()
        {
            if (playerIsland == null) return islandSpawnPos;
            if (hasCustomIslandSpawnPos)
            {
                if (TryGetSurfaceYAtOnActiveIsland(customIslandSpawnPos.x, customIslandSpawnPos.z, out float groundY))
                    return new Vector3(customIslandSpawnPos.x, groundY, customIslandSpawnPos.z);
                hasCustomIslandSpawnPos = false;
            }
            if (returnToLastIslandPosition && hasIslandSpawnPos)
            {
                if (TryGetSurfaceYAtOnActiveIsland(islandSpawnPos.x, islandSpawnPos.z, out float groundY))
                    return new Vector3(islandSpawnPos.x, groundY, islandSpawnPos.z);
                hasIslandSpawnPos = false;
            }
            float centerX = playerIsland.TotalX / 2f;
            float centerZ = playerIsland.TotalZ / 2f;

            int maxRadius = Mathf.CeilToInt(Mathf.Max(playerIsland.TotalX, playerIsland.TotalZ) * 0.5f);
            float islandBaseY = playerIsland.transform.position.y - playerIsland.TotalY;

            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius)
                            continue;

                        float candidateGridX = centerX + dx;
                        float candidateGridZ = centerZ + dz;
                        Vector3 candidatePoint = playerIsland.transform.TransformPoint(new Vector3(candidateGridX, -LobbyFloorY, candidateGridZ));

                        if (!TryGetSurfaceYAtOnActiveIsland(candidatePoint.x, candidatePoint.z, out float candidateGroundY))
                            continue;

                        // Ignore points deep inside the mine shaft and prefer actual top-side ground.
                        if (candidateGroundY <= islandBaseY + 2f)
                            continue;

                        return new Vector3(candidatePoint.x, candidateGroundY, candidatePoint.z);
                    }
                }
            }

            Vector3 defaultPoint = playerIsland.transform.TransformPoint(new Vector3(centerX, -LobbyFloorY, centerZ));
            if (TryGetSurfaceYAtOnActiveIsland(defaultPoint.x, defaultPoint.z, out float dgY))
                return new Vector3(defaultPoint.x, dgY, defaultPoint.z);
            return defaultPoint;
        }

        private bool TryGetSurfaceYAtOnActiveIsland(float worldX, float worldZ, out float groundY)
        {
            groundY = 0f;
            if (ActiveIsland == null) return false;

            MeshCollider c = ActiveIsland.GetComponent<MeshCollider>();
            if (c != null && c.enabled)
            {
                Ray r = new Ray(new Vector3(worldX, ActiveIsland.transform.position.y + ActiveIsland.TotalY + 10f, worldZ), Vector3.down);
                if (c.Raycast(r, out RaycastHit h, ActiveIsland.TotalY + 40f))
                {
                    groundY = h.point.y;
                    return true;
                }
            }

            Vector3 local = ActiveIsland.transform.InverseTransformPoint(new Vector3(worldX, ActiveIsland.transform.position.y, worldZ));
            int gx = Mathf.FloorToInt(local.x);
            int gz = Mathf.FloorToInt(local.z);
            if (gx >= 0 && gz >= 0 && gx < ActiveIsland.TotalX && gz < ActiveIsland.TotalZ)
            {
                for (int gy = 0; gy < ActiveIsland.TotalY; gy++)
                {
                    if (ActiveIsland.IsInBounds(gx, gy, gz) && ActiveIsland.IsSolid(gx, gy, gz))
                    {
                        groundY = ActiveIsland.transform.TransformPoint(ActiveIsland.GridToLocal(gx, gy, gz)).y;
                        return true;
                    }
                }
            }
            return false;
        }

        public void SpawnPlayerAt(Vector3 pos)
        {
            Transform player = ResolveOrSpawnPlayer();
            if (player == null) return;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            Physics.SyncTransforms();
            player.position = new Vector3(pos.x, ComputePlayerSpawnY(player, pos.y), pos.z);
            Physics.SyncTransforms();

            if (!IsInLobbyMode && ActiveMine != null)
            {
                Vector3 worldMineCenter = ActiveIsland.transform.TransformPoint(ActiveIsland.GridToLocal(ActiveMine.originX, LobbyFloorY, ActiveMine.originZ));
                Vector3 lookDir = worldMineCenter - player.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.0001f) player.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
            if (cc != null) StartCoroutine(ReenableCharacterControllerNextFixedUpdate(cc));
        }

        private float ComputePlayerSpawnY(Transform player, float groundY)
        {
            return groundY + playerSpawnHeight + Mathf.Max(0f, safeSpawnDropOffset);
        }

        private System.Collections.IEnumerator ReenableCharacterControllerNextFixedUpdate(CharacterController cc)
        {
            yield return new WaitForFixedUpdate();

            if (cc == null)
                yield break;

            cc.enabled = true;
        }

        public Transform ResolveOrSpawnPlayer()
        {
            if (playerToPlace != null) return playerToPlace;
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) return tagged.transform;
            if (playerPrefab == null) return null;
            GameObject s = Instantiate(playerPrefab); s.tag = "Player"; return s.transform;
        }
    }
}
