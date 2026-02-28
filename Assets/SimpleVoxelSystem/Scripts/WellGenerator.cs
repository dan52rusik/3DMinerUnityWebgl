using System;
using System.Collections.Generic;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Генератор острова. Заполняет VoxelIsland данными без отдельных блок-объектов.
    /// Поддерживает два независимых мира: Лобби и Личный Остров.
    /// </summary>
    [RequireComponent(typeof(VoxelIsland))]
    public class WellGenerator : MonoBehaviour
    {
        [Header("Размер колодца")]
        public int wellWidth = 5;
        public int wellLength = 5;
        public int wellDepth = 10;

        [Header("Паддинг земли вокруг колодца")]
        public int padding = 5;

        [Header("Лобби (спавн-зона)")] 
        [Tooltip("Ширина (X) плоской лобби-платформы в блоках.")]
        public int lobbyWidth  = 50;
        [Tooltip("Длина (Z) плоской лобби-платформы в блоках.")]
        public int lobbyLength = 50;
        [Tooltip("Сколько слоёв можно построить НАД полом. Пол располагается на gridY=lobbyBuildAbove, выше — gridY=0..lobbyBuildAbove-1.")]
        public int lobbyBuildAbove = 8;

        [Header("Блоки")]
        public List<BlockData> blockDataConfig;

        [Header("Mining Rules")]
        public bool lockDeeperLayersUntilCleared = true;

        [Header("Elevator")]
        public Color elevatorColor = new Color(0.6f, 0.3f, 0.1f, 1f);

        [Header("Player Spawn")]
        public GameObject playerPrefab;
        public Transform playerToPlace;
        public float playerSpawnHeight = 1.05f;

        // ─── Runtime ────────────────────────────────────────────────────────
        public bool IsMineGenerated { get; private set; }
        public MineInstance ActiveMine { get; private set; }
        public int LobbyFloorY => lobbyBuildAbove;

        public event System.Action OnFlatPlotReady;
        public event Action<bool> OnWorldSwitch; // true=лобби, false=шахта
        public bool IsInLobbyMode { get; private set; } = true;

        [Header("Private Island Offset")]
        public Vector3 privateIslandOffset = new Vector3(500f, 0f, 500f);

        private Vector3 lobbySpawnPos;
        private Vector3 islandSpawnPos;

        private const int TopLayerDepth = 3;
        private const int MidLayerDepth = 7;

        private VoxelIsland lobbyIsland;
        private VoxelIsland playerIsland;

        public VoxelIsland ActiveIsland => IsInLobbyMode ? lobbyIsland : (playerIsland ?? lobbyIsland);
        public bool IsIslandGenerated => playerIsland != null;

        void Awake()
        {
            // Находим Лобби
            lobbyIsland = GetComponent<VoxelIsland>();
            if (lobbyIsland == null) lobbyIsland = GetComponentInChildren<VoxelIsland>();
            
            // Если лобби всё ещё не найдено — вешаем на себя (старый способ)
            if (lobbyIsland == null) lobbyIsland = gameObject.AddComponent<VoxelIsland>();

            EnsureBasicBlockConfig();

            Transform p = ResolveOrSpawnPlayer();
            if (p != null) lobbySpawnPos = p.position;
            else lobbySpawnPos = Vector3.zero;

            if (GetComponent<MineMarket>() == null)
            {
                gameObject.AddComponent<MineMarket>();
            }
        }

        void Start()
        {
            IsInLobbyMode = true;
            GenerateFlatPlot();
        }

        private void SyncColorsToActiveIsland()
        {
            if (ActiveIsland == null) return;
            
            int typeCount = System.Enum.GetValues(typeof(BlockType)).Length;
            Color[] cols = new Color[typeCount];
            
            // Default colors from island component
            for (int i = 0; i < typeCount && i < ActiveIsland.blockColors.Length; i++)
                cols[i] = ActiveIsland.blockColors[i];

            // Override from config
            if (blockDataConfig != null)
            {
                foreach (BlockData bd in blockDataConfig)
                {
                    int idx = (int)bd.type;
                    if (idx >= 0 && idx < typeCount)
                        if (bd.blockColor.a > 0.01f) cols[idx] = bd.blockColor;
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

                this.transform.position = Vector3.zero;
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
                
                // Центрируем спавн в Лобби (World Y = -floorY)
                lobbySpawnPos = new Vector3(lw / 2f, -floorY, ll / 2f);
                SpawnPlayerAt(lobbySpawnPos);

                Debug.Log("[WellGenerator] Лобби готово.");
                OnFlatPlotReady?.Invoke(); 
            }
            else
            {
                lobbyIsland.gameObject.SetActive(false);
                if (playerIsland == null) CreatePlayerIsland();

                playerIsland.gameObject.SetActive(true);
                this.transform.position = privateIslandOffset;
                Physics.SyncTransforms();

                SyncColorsToActiveIsland();
                
                if (ActiveMine != null)
                {
                    ApplyMineVoxels(ActiveMine);
                    RestoreElevatorForActiveMine();
                }
            }
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
            Debug.Log($"[WellGenerator] Личный Остров СОЗДАН в {privateIslandOffset}.");
        }

        private void RestoreElevatorForActiveMine()
        {
            if (ActiveMine == null) return;
            int ww = ActiveMine.shopData.wellWidth;
            int wl = ActiveMine.shopData.wellLength;
            int pad = ActiveMine.shopData.padding;
            int x0 = ActiveMine.originX - (ww / 2) - pad;
            int z0 = ActiveMine.originZ - (wl / 2) - pad;
            CreateElevator(x0, z0);
        }

        public void GenerateMine(MineInstance mine) => GenerateMineAt(mine, ActiveIsland.TotalX / 2, ActiveIsland.TotalZ / 2);

        public void GenerateMineAt(MineInstance mine, int gx, int gz)
        {
            if (ActiveIsland == null) return;
            
            Transform player = ResolveOrSpawnPlayer();
            CharacterController cc = (player != null) ? player.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            ActiveMine = mine;
            ActiveMine.originX = gx;
            ActiveMine.originZ = gz;
            IsMineGenerated = true;

            ApplyMineVoxels(ActiveMine);

            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int pad = mine.shopData.padding;
            int x0 = gx - (ww / 2) - pad;
            int z0 = gz - (wl / 2) - pad;
            CreateElevator(x0, z0);
            
            Vector3 worldSurfacePos = ActiveIsland.transform.TransformPoint(ActiveIsland.GridToLocal(gx, LobbyFloorY, gz));
            SpawnPlayerAt(new Vector3(worldSurfacePos.x, worldSurfacePos.y, worldSurfacePos.z - 3f));

            IsInLobbyMode = false;
            OnWorldSwitch?.Invoke(false);
            Debug.Log($"[WellGenerator] Шахта '{mine.shopData.displayName}' построена в ({gx},{gz}).");
        }

        private void ApplyMineVoxels(MineInstance mine)
        {
            if (mine == null || ActiveIsland == null) return;

            int gx = mine.originX;
            int gz = mine.originZ;
            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int wd = mine.rolledDepth;
            int pad = mine.shopData.padding;

            int x0 = gx - (ww / 2) - pad;
            int z0 = gz - (wl / 2) - pad;

            int shaftX = x0; 
            int shaftZ = z0; 

            if (!mine.HasVoxelsData)
                mine.InitializeVoxels(ww + pad * 2, wl + pad * 2, wd);

            int actualBlocksCount = 0;
            for (int ix = 0; ix < ww + pad * 2; ix++)
            for (int iz = 0; iz < wl + pad * 2; iz++)
            {
                int curX = x0 + ix;
                int curZ = z0 + iz;
                if (!ActiveIsland.IsInBounds(curX, 0, curZ)) continue;

                for (int iy = 0; iy < wd; iy++)
                {
                    int curY = LobbyFloorY + iy;
                    if (curY >= ActiveIsland.TotalY) break;

                    if (curX == shaftX && curZ == shaftZ)
                    {
                        ActiveIsland.RemoveVoxel(curX, curY, curZ, false);
                        continue;
                    }

                    bool inWell = ix >= pad && ix < ww + pad && iz >= pad && iz < wl + pad;

                    if (inWell && mine.IsVoxelMined(curX, curY, curZ))
                    {
                        ActiveIsland.RemoveVoxel(curX, curY, curZ, false);
                        continue;
                    }

                    BlockType t;
                    if (mine.GetVoxel(ix, iy, iz) == BlockType.Air) 
                    {
                        t = inWell ? mine.shopData.RollBlockType(iy) : BlockType.Dirt;
                        mine.SetVoxel(ix, iy, iz, t);
                    }
                    else
                    {
                        t = mine.GetVoxel(ix, iy, iz);
                    }

                    ActiveIsland.SetVoxel(curX, curY, curZ, t);
                    if (inWell) actualBlocksCount++;
                }
            }

            if (mine.totalBlocks <= 0) mine.totalBlocks = actualBlocksCount;
            ActiveIsland.RebuildMesh();
        }

        private void SetIslandActive(VoxelIsland island, bool active)
        {
            if (island == null) return;
            
            // Если остров и скрипты на одном объекте — нельзя гасить весь GO!
            if (island.gameObject == this.gameObject)
            {
                var renderer = island.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = active;
                var collider = island.GetComponent<MeshCollider>();
                if (collider != null) collider.enabled = active;
            }
            else
            {
                island.gameObject.SetActive(active);
            }
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

            SetIslandActive(lobbyIsland, false);
            SetIslandActive(playerIsland, true);

            this.transform.position = privateIslandOffset;
            Physics.SyncTransforms();

            if (ActiveMine != null)
            {
                Vector3 worldMinePos = playerIsland.transform.TransformPoint(playerIsland.GridToLocal(ActiveMine.originX, LobbyFloorY, ActiveMine.originZ));
                SpawnPlayerAt(new Vector3(worldMinePos.x, worldMinePos.y, worldMinePos.z - 3f));
            }
            else
            {
                float centerX = playerIsland.TotalX / 2f;
                float centerZ = playerIsland.TotalZ / 2f;
                // Спаун в центре острова в локальных координатах
                SpawnPlayerAt(playerIsland.transform.TransformPoint(new Vector3(centerX, -LobbyFloorY, centerZ)));
            }

            OnWorldSwitch?.Invoke(false);
        }

        public void SwitchToLobby()
        {
            if (IsInLobbyMode) return;
            IsInLobbyMode = true;

            SetIslandActive(playerIsland, false);
            SetIslandActive(lobbyIsland, true);

            this.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            // Всегда спавним в центре Лобби
            float cx = lobbyWidth / 2f;
            float cz = lobbyLength / 2f;
            SpawnPlayerAt(new Vector3(cx, -LobbyFloorY, cz));

            OnWorldSwitch?.Invoke(true);
        }

        public void DemolishMine()
        {
            if (!IsMineGenerated) return;

            foreach (SimpleElevator elev in GetComponentsInChildren<SimpleElevator>())
                Destroy(elev.gameObject);

            ActiveMine = null;
            IsMineGenerated = false;

            GenerateFlatPlot();
        }

        public void MineVoxel(int gx, int gy, int gz)
        {
            ActiveIsland.RemoveVoxel(gx, gy, gz);

            if (ActiveMine != null && IsInsideWellArea(gx, gz))
            {
                ActiveMine.RegisterMinedBlock(gx, gy, gz);
            }
        }

        public bool IsInsideWellArea(int gx, int gz)
        {
            if (ActiveMine == null) return false;
            int ww = ActiveMine.shopData.wellWidth;
            int ox = ActiveMine.originX;
            int oz = ActiveMine.originZ;
            int xMin = ox - (ww / 2);
            int zMin = oz - (ActiveMine.shopData.wellLength / 2);
            return gx >= xMin && gx < xMin + ww && gz >= zMin && gz < zMin + ActiveMine.shopData.wellLength;
        }

        public bool IsOnWellRimSurface(int gx, int gy, int gz)
        {
            if (ActiveMine == null) return false;
            if (gy != LobbyFloorY) return false;
            int pad = ActiveMine.shopData.padding;
            int ww = ActiveMine.shopData.wellWidth;
            int wl = ActiveMine.shopData.wellLength;
            int ox = ActiveMine.originX;
            int oz = ActiveMine.originZ;
            int xMin = ox - (ww / 2) - pad;
            int zMin = oz - (wl / 2) - pad;
            return gx >= xMin && gx < xMin + ww + pad * 2 && gz >= zMin && gz < zMin + wl + pad * 2;
        }

        public bool CanMineVoxel(int gx, int gy, int gz)
        {
            if (!IsMineGenerated || ActiveMine == null) return false;
            int mineDepth = ActiveMine.rolledDepth;
            if (gy < LobbyFloorY || gy >= LobbyFloorY + mineDepth) return false;
            if (!IsInsideWellArea(gx, gz) && !IsOnWellRimSurface(gx, gy, gz)) return false;
            if (!lockDeeperLayersUntilCleared) return true;
            if (!IsInsideWellArea(gx, gz)) return true;
            if (gy <= LobbyFloorY) return true;
            return IsWellLayerCleared(gy - 1);
        }

        public bool IsWellLayerCleared(int depthGridY)
        {
            if (ActiveMine == null) return false;
            int ww = ActiveMine.shopData.wellWidth;
            int wl = ActiveMine.shopData.wellLength;
            int ox = ActiveMine.originX;
            int oz = ActiveMine.originZ;
            int xMin = ox - (ww / 2);
            int zMin = oz - (wl / 2);

            for (int x = xMin; x < xMin + ww; x++)
            for (int z = zMin; z < zMin + wl; z++)
            {
                if (ActiveIsland.IsInBounds(x, depthGridY, z) && ActiveIsland.IsSolid(x, depthGridY, z))
                    return false;
            }
            return true;
        }

        public int GetContiguousClearedDepth()
        {
            if (ActiveMine == null) return 0;
            int cleared = 0;
            for (int y = 0; y < ActiveMine.rolledDepth; y++)
            {
                if (!IsWellLayerCleared(LobbyFloorY + y)) break;
                cleared++;
            }
            return cleared;
        }

        public void GeneratePlotExtension(int offsetX, int offsetZ, int width, int length)
        {
            Debug.Log($"[WellGenerator] Покупка участка +[{offsetX},{offsetZ}] size {width}x{length}");
        }

        public void SpawnPlayerAt(Vector3 pos)
        {
            Transform player = ResolveOrSpawnPlayer();
            if (player == null) return;

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            Physics.SyncTransforms();
            float spawnY = ComputePlayerSpawnY(player, pos.y);
            player.position = new Vector3(pos.x, spawnY, pos.z);
            Physics.SyncTransforms();

            if (ActiveMine != null)
            {
                Vector3 worldMineCenter = ActiveIsland.transform.TransformPoint(ActiveIsland.GridToLocal(ActiveMine.originX, LobbyFloorY, ActiveMine.originZ));
                Vector3 lookDir = worldMineCenter - player.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.0001f)
                    player.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            if (cc != null) cc.enabled = true;
        }

        private float GetGroundYAt(float worldX, float worldZ)
        {
            MeshCollider islandCollider = ActiveIsland.GetComponent<MeshCollider>();
            Vector3 rayOrigin = new Vector3(worldX, ActiveIsland.transform.position.y + ActiveIsland.TotalY + 10f, worldZ);
            Ray ray = new Ray(rayOrigin, Vector3.down);
            if (islandCollider != null && islandCollider.Raycast(ray, out RaycastHit hit, ActiveIsland.TotalY + 30f))
                return hit.point.y;
            return ActiveIsland.transform.position.y;
        }

        private float ComputePlayerSpawnY(Transform player, float groundY)
        {
            const float clearance = 1.0f;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                float bottomOffset = cc.center.y - (cc.height * 0.5f) + cc.skinWidth;
                return groundY + clearance - bottomOffset;
            }
            return groundY + playerSpawnHeight;
        }

        private Transform ResolveOrSpawnPlayer()
        {
            if (playerToPlace != null) return playerToPlace;
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null) return taggedPlayer.transform;
            if (playerPrefab == null) return null;
            GameObject spawned = Instantiate(playerPrefab);
            spawned.tag = "Player";
            return spawned.transform;
        }

        void CreateElevator(int gx, int gz)
        {
            foreach (var old in GetComponentsInChildren<SimpleElevator>()) Destroy(old.gameObject);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "AutoElevator";
            go.transform.SetParent(this.transform);
            
            Vector3 localPos = ActiveIsland.GridToLocal(gx, LobbyFloorY, gz);
            go.transform.position = ActiveIsland.transform.TransformPoint(localPos + new Vector3(0.5f, 0.125f, 0.5f));
            go.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);

            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 2f, 1f);
            trigger.center = new Vector3(0f, 1f, 0f);

            SimpleElevator elevatorScript = go.AddComponent<SimpleElevator>();
            elevatorScript.topY = go.transform.position.y;
            elevatorScript.wellGenerator = this;
            elevatorScript.island = ActiveIsland;
            elevatorScript.shaftGridX = gx; 
            elevatorScript.shaftGridZ = gz;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = elevatorColor;
                renderer.material = mat;
            }
        }

        private void EnsureBasicBlockConfig()
        {
            if (blockDataConfig == null) blockDataConfig = new List<BlockData>();
            bool hasGrass = false, hasDirt = false;
            foreach (var b in blockDataConfig)
            {
                if (b.type == BlockType.Grass) hasGrass = true;
                if (b.type == BlockType.Dirt) hasDirt = true;
            }
            if (!hasGrass) blockDataConfig.Add(new BlockData { type = BlockType.Grass, blockColor = new Color(0.2f, 0.8f, 0.2f, 1f), maxHealth = 3 });
            if (!hasDirt) blockDataConfig.Add(new BlockData { type = BlockType.Dirt, blockColor = new Color(0.5f, 0.3f, 0.1f, 1f), maxHealth = 3 });
        }
    }
}
