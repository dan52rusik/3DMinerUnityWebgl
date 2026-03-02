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

        [Header("Lobby Persistence")]
        [Tooltip("Лобби всегда остается в сцене. При переходе на остров скрывается только визуально по дистанции.")]
        public bool keepLobbyAlwaysLoaded = true;
        [Tooltip("Если игрок дальше этой дистанции от центра лобби, лобби можно не рендерить.")]
        public float lobbyRenderDistance = 100f;
        [Tooltip("Выключать ли коллайдер лобби, когда лобби скрыто по дистанции.")]
        public bool disableLobbyColliderWhenFar = true;

        [Header("Блоки")]
        public List<BlockData> blockDataConfig;

        [Header("Mining Rules")]
        public bool lockDeeperLayersUntilCleared = false; // Отключаем по умолчанию для удобства

        [Header("Elevator")]
        public Color elevatorColor = new Color(0.6f, 0.3f, 0.1f, 1f);

        [Header("Player Spawn")]
        public GameObject playerPrefab;
        public Transform playerToPlace;
        public float playerSpawnHeight = 1.05f;
        [Tooltip("При установке шахты не телепортировать игрока в центр шахты.")]
        public bool keepPlayerPositionWhenPlacingMine = true;
        [Tooltip("При возврате из лобби на остров возвращать игрока в последнюю позицию на острове.")]
        public bool returnToLastIslandPosition = true;

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
        private bool hasIslandSpawnPos;

        private const int TopLayerDepth = 3;
        private const int MidLayerDepth = 7;

        private VoxelIsland lobbyIsland;
        private VoxelIsland playerIsland;
        private MeshRenderer lobbyRenderer;
        private MeshCollider lobbyCollider;

        public VoxelIsland ActiveIsland => IsInLobbyMode ? lobbyIsland : (playerIsland ?? lobbyIsland);
        public bool IsIslandGenerated => playerIsland != null;

        void Awake()
        {
            // Находим Лобби
            lobbyIsland = GetComponent<VoxelIsland>();
            if (lobbyIsland == null) lobbyIsland = GetComponentInChildren<VoxelIsland>();
            
            // Если лобби всё ещё не найдено — вешаем на себя (старый способ)
            if (lobbyIsland == null) lobbyIsland = gameObject.AddComponent<VoxelIsland>();
            lobbyRenderer = lobbyIsland.GetComponent<MeshRenderer>();
            lobbyCollider = lobbyIsland.GetComponent<MeshCollider>();

            EnsureBasicBlockConfig();

            if (GetComponent<PickaxeShopUI>() == null)
                gameObject.AddComponent<PickaxeShopUI>();

            Transform p = ResolveOrSpawnPlayer();
            if (p != null) lobbySpawnPos = p.position;
            else lobbySpawnPos = Vector3.zero;

            if (GetComponent<MineMarket>() == null)
            {
                gameObject.AddComponent<MineMarket>();
            }

            // Принудительно отключаем блокировку слоев, чтобы можно было копать сразу вниз
            lockDeeperLayersUntilCleared = false;
        }

        void Start()
        {
            IsInLobbyMode = true;
            GenerateFlatPlot();
        }

        void Update()
        {
            UpdateLobbyStreamingVisibility();
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
                if (keepLobbyAlwaysLoaded) lobbyIsland.gameObject.SetActive(true);
                else lobbyIsland.gameObject.SetActive(false);
                if (playerIsland == null) CreatePlayerIsland();

                playerIsland.gameObject.SetActive(true);
                Physics.SyncTransforms();

                SyncColorsToActiveIsland();
                
                if (ActiveMine != null)
                {
                    ApplyMineVoxels(ActiveMine);
                    RestoreElevatorForActiveMine();
                }
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
            if (mine == null || mine.shopData == null) return;
            
            Transform player = ResolveOrSpawnPlayer();
            CharacterController cc = (player != null) ? player.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            int minDepth = Mathf.Min(mine.shopData.depthMin, mine.shopData.depthMax);
            int maxDepth = Mathf.Max(mine.shopData.depthMin, mine.shopData.depthMax);
            mine.rolledDepth = Mathf.Clamp(mine.rolledDepth, minDepth, maxDepth);

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
            // Возвращаем лифт в угол зоны, как просил пользователь
            CreateElevator(x0, z0);
            
            if (!keepPlayerPositionWhenPlacingMine)
            {
                Vector3 worldSurfacePos = ActiveIsland.transform.TransformPoint(ActiveIsland.GridToLocal(gx, LobbyFloorY, gz));
                SpawnPlayerAt(new Vector3(worldSurfacePos.x, worldSurfacePos.y, worldSurfacePos.z - 3f));
            }
            else
            {
                RememberCurrentIslandSpawnPoint();
                if (cc != null) cc.enabled = true;
            }

            IsInLobbyMode = false;
            OnWorldSwitch?.Invoke(false);
            AsyncGameplayEvents.PublishWorldSwitch(false);
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

            // Расчистка под лифтом теперь тоже в углу
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
                    if (!mine.HasVoxelValue(ix, iy, iz))
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

            if (keepLobbyAlwaysLoaded) SetIslandActive(lobbyIsland, true);
            else SetIslandActive(lobbyIsland, false);
            SetIslandActive(playerIsland, true);

            Physics.SyncTransforms();

            if (ActiveMine != null)
            {
                ApplyMineVoxels(ActiveMine);
                RestoreElevatorForActiveMine();
            }

            if (returnToLastIslandPosition && hasIslandSpawnPos)
            {
                SpawnPlayerAt(islandSpawnPos);
            }
            else if (ActiveMine != null)
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
            AsyncGameplayEvents.PublishWorldSwitch(false);
            UpdateLobbyStreamingVisibility();
        }

        public void SwitchToLobby()
        {
            if (IsInLobbyMode) return;
            IsInLobbyMode = true; // Сначала ставим флаг

            RememberCurrentIslandSpawnPoint();
            SetIslandActive(playerIsland, false);
            SetIslandActive(lobbyIsland, true);
            Physics.SyncTransforms();

            // Всегда спавним в центре Лобби
            float cx = lobbyWidth / 2f;
            float cz = lobbyLength / 2f;
            SpawnPlayerAt(new Vector3(cx, -LobbyFloorY, cz));

            OnWorldSwitch?.Invoke(true);
            AsyncGameplayEvents.PublishWorldSwitch(true);
            UpdateLobbyStreamingVisibility();
        }

        public void EnsurePrivateIslandAtOffset(Vector3 offset)
        {
            privateIslandOffset = offset;

            if (playerIsland == null)
                CreatePlayerIsland();

            SetIslandActive(playerIsland, !IsInLobbyMode);
            SetIslandActive(lobbyIsland, true);
            UpdateLobbyStreamingVisibility();
        }

        public void RestoreMineFromSave(MineInstance mine)
        {
            ActiveMine = mine;
            IsMineGenerated = mine != null;

            if (ActiveMine == null || IsInLobbyMode)
                return;

            ApplyMineVoxels(ActiveMine);
            RestoreElevatorForActiveMine();
        }

        private void RememberCurrentIslandSpawnPoint()
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

        private void UpdateLobbyStreamingVisibility()
        {
            if (lobbyIsland == null || lobbyRenderer == null) return;

            bool shouldShow = true;
            if (keepLobbyAlwaysLoaded && !IsInLobbyMode)
            {
                Transform player = ResolveOrSpawnPlayer();
                if (player != null)
                {
                    Vector3 lobbyCenter = lobbyIsland.transform.TransformPoint(
                        new Vector3(lobbyWidth * 0.5f, -LobbyFloorY, lobbyLength * 0.5f));
                    float maxDist = Mathf.Max(1f, lobbyRenderDistance);
                    shouldShow = (player.position - lobbyCenter).sqrMagnitude <= maxDist * maxDist;
                }
            }

            if (!keepLobbyAlwaysLoaded)
                shouldShow = IsInLobbyMode;

            lobbyRenderer.enabled = shouldShow;

            if (lobbyCollider != null)
            {
                bool keepCollider = IsInLobbyMode || shouldShow || !disableLobbyColliderWhenFar;
                lobbyCollider.enabled = keepCollider;
            }
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
            if (ActiveIsland == null) return;
            ActiveIsland.RemoveVoxel(gx, gy, gz);

            // Проверка разрушения лифта: 
            // Он ломается только если мы ударили блок прямо ПОД ним (на его текущей высоте)
            // И если копать глубже уже нельзя.
            foreach (SimpleElevator elev in GetComponentsInChildren<SimpleElevator>())
            {
                if (elev != null && elev.shaftGridX == gx && elev.shaftGridZ == gz)
                {
                    // Вычисляем текущий Grid Y лифта
                    Vector3 localPos = ActiveIsland.transform.InverseTransformPoint(elev.transform.position);
                    int elevGridY = ActiveIsland.LocalToGrid(localPos).y;

                    // Если бьем именно тот блок, на котором он стоит
                    if (gy == elevGridY)
                    {
                        int clearedDepth = GetContiguousClearedDepth();
                        int maxDepth = ActiveMine != null ? ActiveMine.rolledDepth : 0;
                        
                        // Если это последний возможный блок в этой шахте — ломаем
                        if (clearedDepth >= maxDepth) 
                        {
                            Destroy(elev.gameObject);
                            Debug.Log($"[WellGenerator] Лифт разрушен: удалена последняя опора.");
                        }
                    }
                }
            }

            if (ActiveMine != null && IsInsideWellArea(gx, gz))
            {
                ActiveMine.RegisterMinedBlock(gx, gy, gz);
            }
        }

        public bool IsInsideWellArea(int gx, int gz)
        {
            if (ActiveMine == null) return false;
            int ww = ActiveMine.shopData.wellWidth;
            int wl = ActiveMine.shopData.wellLength;
            int pad = ActiveMine.shopData.padding;
            int ox = ActiveMine.originX;
            int oz = ActiveMine.originZ;

            int xMin = ox - (ww / 2) - pad;
            int zMin = oz - (wl / 2) - pad;
            int xMax = xMin + ww + pad * 2 - 1;
            int zMax = zMin + wl + pad * 2 - 1;

            return gx >= xMin && gx <= xMax && gz >= zMin && gz <= zMax;
        }

        public bool CanMineVoxel(int gx, int gy, int gz)
        {
            if (IsInLobbyMode) return false;

            // Если шахта не куплена/не поставлена, разрешаем копать верхние 3 слоя земли (терраформинг)
            if (!IsMineGenerated || ActiveMine == null)
            {
                return (gy >= LobbyFloorY && gy < LobbyFloorY + 3);
            }
            
            int mineDepth = ActiveMine.rolledDepth;
            // Нельзя копать воздух выше земли
            if (gy < LobbyFloorY) return false;
            // Нельзя копать глубже, чем рассчитано для этой шахты
            if (gy >= LobbyFloorY + mineDepth) return false;

            // Пользователь разрешил копать ВЕЗДЕ на острове (стен больше нет)
            // Поэтому IsInsideWellArea больше не ограничивает копание.

            // Если блокировка слоев отключена — копаем свободно
            if (!lockDeeperLayersUntilCleared) return true;

            // Иначе проверяем слой выше
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

            if (!IsInLobbyMode && ActiveMine != null)
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

            // Лифт не должен блокировать луч кирки (Layer 2 = Ignore Raycast)
            go.layer = 2;

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
            
            ForceUpdateOrAdd(BlockType.Grass, new Color(0.2f, 0.8f, 0.2f), 1, 2, 0);
            ForceUpdateOrAdd(BlockType.Dirt,  new Color(0.5f, 0.3f, 0.1f), 1, 2, 0);
            ForceUpdateOrAdd(BlockType.Stone, new Color(0.5f, 0.5f, 0.5f), 2, 10, 5); // LVL 5
            ForceUpdateOrAdd(BlockType.Iron,  new Color(0.8f, 0.6f, 0.4f), 5, 50, 10); // LVL 10
            ForceUpdateOrAdd(BlockType.Gold,  new Color(1.0f, 0.9f, 0.0f), 10, 100, 15); // LVL 15
        }

        private void ForceUpdateOrAdd(BlockType type, Color color, int hp, int xp, int level)
        {
            BlockData existing = null;
            if (blockDataConfig != null)
            {
                foreach (var b in blockDataConfig) 
                {
                    if (b != null && b.type == type) 
                    {
                        existing = b;
                        break;
                    }
                }
            }

            if (existing != null)
            {
                // Принудительно обновляем требования и награду, чтобы изменения в коде сработали
                existing.xpReward = xp;
                existing.requiredMiningLevel = level;
                existing.maxHealth = hp;
                existing.reward = xp * 2;
            }
            else
            {
                blockDataConfig.Add(new BlockData 
                { 
                    type = type, 
                    blockColor = color, 
                    maxHealth = hp, 
                    xpReward = xp, 
                    requiredMiningLevel = level,
                    reward = xp * 2
                });
            }
        }
    }

    public static class RuntimeUiFont
    {
        private static Font cached;

        public static Font Get()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<Font>("Roboto-Regular");
            if (cached != null)
                return cached;

            try
            {
                cached = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "Tahoma", "Verdana" },
                    16
                );
            }
            catch
            {
                cached = null;
            }

            if (cached == null)
                cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return cached;
        }
    }

    public enum AsyncGameplayEventType
    {
        MineBlock,
        SellBackpack,
        BuyMine,
        PlaceMine,
        SellMine,
        WorldSwitch
    }

    public struct AsyncGameplayEvent
    {
        public AsyncGameplayEventType Type;
        public int gx;
        public int gy;
        public int gz;
        public int moneyDelta;
        public int xpDelta;
        public int miningLevel;
        public bool inLobby;
        public int blockType;
        public int mineDepth;
        public string mineName;
    }

    public static class AsyncGameplayEvents
    {
        public static Action<AsyncGameplayEvent> OnEvent;

        public static void PublishMineBlock(int gx, int gy, int gz, BlockType blockType, int xp, bool inLobby)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.MineBlock,
                gx = gx,
                gy = gy,
                gz = gz,
                xpDelta = xp,
                inLobby = inLobby,
                blockType = (int)blockType,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishSellBackpack(int moneyDelta)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.SellBackpack,
                moneyDelta = moneyDelta,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishBuyMine(string mineName, int mineDepth, int moneyDelta)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.BuyMine,
                mineName = mineName ?? string.Empty,
                mineDepth = mineDepth,
                moneyDelta = moneyDelta,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishPlaceMine(string mineName, int gx, int gz)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.PlaceMine,
                mineName = mineName ?? string.Empty,
                gx = gx,
                gz = gz,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishSellMine(string mineName, int moneyDelta)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.SellMine,
                mineName = mineName ?? string.Empty,
                moneyDelta = moneyDelta,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishWorldSwitch(bool inLobby)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.WorldSwitch,
                inLobby = inLobby,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }
    }
}
