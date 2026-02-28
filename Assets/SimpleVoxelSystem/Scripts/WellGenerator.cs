using System;
using System.Collections.Generic;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Генератор острова. Заполняет VoxelIsland данными без отдельных блок-объектов.
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
        /// <summary>true после вызова GenerateMine()</summary>
        public bool IsMineGenerated { get; private set; }

        /// <summary>Текущая активная шахта (null если не куплена)</summary>
        public MineInstance ActiveMine { get; private set; }

        /// <summary>Грид-Y уровня пола в лобби.
        /// Слои 0..(LobbyFloorY-1) — пространство над полом для постройки.
        /// LobbyEditor использует это значение для определения базовых блоков.</summary>
        public int LobbyFloorY => lobbyBuildAbove;

        /// <summary>Срабатывает каждый раз после генерации лобби-площадки.
        /// LobbyEditor подписывается, чтобы загрузить сохранённые блоки.</summary>
        public event System.Action OnFlatPlotReady;
        public event Action<bool> OnWorldSwitch; // true=лобби, false=шахта
        public bool IsInLobbyMode { get; private set; } = true;
        private Vector3 lobbySpawnPos;
        private Vector3 islandSpawnPos;
        private static readonly Vector3 PrivateIslandPos = new Vector3(500f, 0f, 500f);

        private const int TopLayerDepth = 3;
        private const int MidLayerDepth = 7;

        private VoxelIsland island;

        public bool IsIslandGenerated { get; private set; } = false;

        void Awake()
        {
            island = GetComponent<VoxelIsland>();

            // Обеспечиваем наличие базовых цветов для новых типов (Air и Grass)
            EnsureBasicBlockConfig();

            // Запоминаем текущую позицию игрока как спавн в Лобби
            Transform p = ResolveOrSpawnPlayer();
            if (p != null) lobbySpawnPos = p.position;
            else lobbySpawnPos = Vector3.zero;

            // Гарантируем наличие MineMarket
            if (GetComponent<MineMarket>() == null)
            {
                gameObject.AddComponent<MineMarket>();
                Debug.Log("[WellGenerator] MineMarket автоздан.");
            }
        }

        void Start()
        {
            // При старте всегда генерируем Лобби-спавн
            IsInLobbyMode = true;
            GenerateFlatPlot();
        }

        /// <summary>
        /// Генерирует плоскую лобби-площадку из блоков Dirt (1 слой).
        /// Размер задаётся значениями lobbyWidth и lobbyLength (независимо от параметров шахты).
        /// Вызывается при старте и после сноса шахты.
        /// </summary>
        public void GenerateFlatPlot()
        {
            if (island == null)
                island = GetComponent<VoxelIsland>();

            IsMineGenerated = false;
            ActiveMine = null;

            if (blockDataConfig != null && blockDataConfig.Count > 0)
                SyncColorsToIsland();

            // Позиционируем объект в зависимости от режима
            this.transform.position = IsInLobbyMode ? Vector3.zero : PrivateIslandPos;
            
            // Если мы в ЛОББИ — генерируем простую площадку (LobbyEditor заполнит остальное)
            if (IsInLobbyMode)
            {
                int lw = Mathf.Max(32, lobbyWidth);
                int ll = Mathf.Max(32, lobbyLength);
                island.Init(lw, lobbyBuildAbove + 1, ll, 0, 0);

                int floorY = lobbyBuildAbove;
                for (int x = 0; x < island.TotalX; x++)
                for (int z = 0; z < island.TotalZ; z++)
                    island.SetVoxel(x, floorY, z, BlockType.Dirt);

                island.RebuildMesh();
                SpawnPlayerAt(new Vector3(lw/2f, 1.25f, ll/2f));

                // IsIslandGenerated больше НЕ устанавливаем здесь, т.к. лобби — это не остров игрока
                Debug.Log("[WellGenerator] Лобби готово (центральный спавн).");
                OnFlatPlotReady?.Invoke(); 
            }
            else
            {
                // Если мы на ОСТРОВЕ — генерируем красивую круглую базу (теперь покрупнее)
                int lw = Mathf.Max(64, lobbyWidth);
                int ll = Mathf.Max(64, lobbyLength);
                int totalHeight = lobbyBuildAbove + 32; 
                island.Init(lw, totalHeight, ll, 0, 0);

                int floorY = lobbyBuildAbove;
                float centerX = lw / 2f;
                float centerZ = ll / 2f;
                float radius = Mathf.Min(lw, ll) / 2.2f;

                for (int x = 0; x < island.TotalX; x++)
                for (int z = 0; z < island.TotalZ; z++)
                {
                    float dx = x - centerX;
                    float dz = z - centerZ;
                    if (dx*dx + dz*dz > radius*radius) continue;

                    island.SetVoxel(x, floorY, z, BlockType.Grass);
                    for (int y = floorY + 1; y < island.TotalY; y++)
                        island.SetVoxel(x, y, z, BlockType.Dirt);
                }

                island.RebuildMesh();
                SpawnPlayerAt(new Vector3(PrivateIslandPos.x + centerX, -floorY + 1.25f, PrivateIslandPos.z + centerZ));
                
                Transform player = ResolveOrSpawnPlayer();
                if (player != null) islandSpawnPos = player.position;

                IsIslandGenerated = true;
                OnWorldSwitch?.Invoke(false);
                Debug.Log($"[WellGenerator] Личный Остров создан в {PrivateIslandPos}.");

                // Если уже есть шахта — восстанавливаем её воксели на новом месте
                if (ActiveMine != null)
                {
                    ApplyMineVoxels(ActiveMine);
                    // Телепортируем к шахте, а не в абстрактный центр
                    Vector3 worldMinePos = island.transform.TransformPoint(island.GridToLocal(ActiveMine.originX, LobbyFloorY, ActiveMine.originZ));
                    SpawnPlayerAt(new Vector3(worldMinePos.x, worldMinePos.y + 1.5f, worldMinePos.z - 3f));
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Публичное API для MineMarket
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Генерирует шахту по данным MineInstance.
        /// Вызывается снаружи (MinePlacement / MineMarket) после подтверждения покупки.
        /// </summary>
        public void GenerateMine(MineInstance mine) => GenerateMineAt(mine, island.TotalX / 2, island.TotalZ / 2);

        public void GenerateMineAt(MineInstance mine, int gx, int gz)
        {
            if (island == null) return;
            
            ActiveMine = mine;
            ActiveMine.originX = gx;
            ActiveMine.originZ = gz;
            IsMineGenerated = true;

            ApplyMineVoxels(ActiveMine);

            // Лифт ставим сбоку от шахты (основываясь на gx, gz)
            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int pad = mine.shopData.padding;
            int x0 = gx - (ww / 2) - pad;
            int z0 = gz - (wl / 2) - pad;
            CreateElevator(x0, z0);
            
            // Телепортируем игрока к новой шахте (мировые координаты!)
            Vector3 worldSurfacePos = island.transform.TransformPoint(island.GridToLocal(gx, LobbyFloorY, gz));
            SpawnPlayerAt(new Vector3(worldSurfacePos.x, worldSurfacePos.y + 1.5f, worldSurfacePos.z - 2f));

            IsInLobbyMode = false;
            OnWorldSwitch?.Invoke(false);

            Debug.Log($"[WellGenerator] Шахта '{mine.shopData.displayName}' построена в ({gx},{gz}).");
        }

        private void ApplyMineVoxels(MineInstance mine)
        {
            if (mine == null || island == null) return;

            int gx = mine.originX;
            int gz = mine.originZ;
            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int wd = mine.rolledDepth;
            int pad = mine.shopData.padding;

            int x0 = gx - (ww / 2) - pad;
            int z0 = gz - (wl / 2) - pad;

            // Координаты шахты лифта (всегда Air, чтобы он мог ездить)
            int shaftX = x0; // x0 из CreateElevator
            int shaftZ = z0; // z0 из CreateElevator

            int blockCount = 0;
            for (int ix = 0; ix < ww + pad * 2; ix++)
            for (int iz = 0; iz < wl + pad * 2; iz++)
            {
                int curX = x0 + ix;
                int curZ = z0 + iz;
                if (!island.IsInBounds(curX, 0, curZ)) continue;

                for (int iy = 0; iy < wd; iy++)
                {
                    int curY = LobbyFloorY + iy;
                    if (curY >= island.TotalY) break;

                    // Если это шахта лифта — всегда воздух
                    if (curX == shaftX && curZ == shaftZ)
                    {
                        island.RemoveVoxel(curX, curY, curZ, false);
                        continue;
                    }

                    bool inWell = ix >= pad && ix < ww + pad && iz >= pad && iz < wl + pad;
                    
                    if (inWell && mine.IsVoxelMined(curX, curY, curZ))
                    {
                        island.RemoveVoxel(curX, curY, curZ, false);
                        continue;
                    }

                    BlockType t = inWell ? mine.shopData.RollBlockType(iy) : BlockType.Dirt;
                    island.SetVoxel(curX, curY, curZ, t);
                    if (inWell) blockCount++;
                }
            }

            mine.totalBlocks = blockCount;
            island.RebuildMesh();
        }

        public void SwitchToLobby()
        {
            if (IsInLobbyMode) return;
            IsInLobbyMode = true;
            GenerateFlatPlot(); // Генерирует Лобби заново
            OnWorldSwitch?.Invoke(true);
        }

        public void SwitchToMine()
        {
            if (!IsInLobbyMode) return;
            IsInLobbyMode = false;
            
            // Если режима острова еще не было (т.е. первый раз нажали "Создать Остров")
            // или если мы просто возвращаемся — зовем генератор
            GenerateFlatPlot(); 
            OnWorldSwitch?.Invoke(false);
        }

        private void TeleportPlayer(Vector3 target)
        {
            Transform player = ResolveOrSpawnPlayer();
            if (player == null) return;

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = target;
            if (cc != null) cc.enabled = true;
        }

        /// <summary>
        /// Разрушает текущую шахту (очищает воксели), готовит участок к новой.
        /// </summary>
        public void DemolishMine()
        {
            if (!IsMineGenerated) return;

            // Удаляем лифт
            foreach (SimpleElevator elev in GetComponentsInChildren<SimpleElevator>())
                Destroy(elev.gameObject);

            // Возвращаем плоскую площадку
            Debug.Log("[WellGenerator] Шахта снесена. Возвращаем площадку.");
            GenerateFlatPlot();
        }

        public void MineVoxel(int gx, int gy, int gz)
        {
            island.RemoveVoxel(gx, gy, gz);

            // Регистрируем добытый блок в активной шахте
            if (ActiveMine != null && IsInsideWellArea(gx, gz))
            {
                ActiveMine.RegisterMinedBlock(gx, gy, gz);
                if (ActiveMine.IsExhausted)
                    Debug.Log($"[WellGenerator] Шахта '{ActiveMine.shopData.displayName}' ИСТОЩЕНА! " +
                              $"Добыто {ActiveMine.minedBlocks}/{ActiveMine.totalBlocks} блоков.");
            }
        }

        public bool IsInsideWellArea(int gx, int gz)
        {
            if (ActiveMine == null) return false;
            
            int ww = ActiveMine.shopData.wellWidth;
            int wl = ActiveMine.shopData.wellLength;
            int ox = ActiveMine.originX;
            int oz = ActiveMine.originZ;

            // Центрируем шахту относительно origin
            int xMin = ox - (ww / 2);
            int xMax = xMin + ww;
            int zMin = oz - (wl / 2);
            int zMax = zMin + wl;

            return gx >= xMin && gx < xMax && gz >= zMin && gz < zMax;
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
            int xMax = xMin + ww + pad; // Note: simplified bounds
            int zMin = oz - (wl / 2) - pad;
            int zMax = zMin + wl + pad;

            return gx >= xMin && gx < xMax && gz >= zMin && gz < zMax;
        }

        public bool CanMineVoxel(int gx, int gy, int gz)
        {
            // На плоской площадке (шахта ещё не куплена) — копать нельзя
            if (!IsMineGenerated)
                return false;

            if (ActiveMine == null) return false;

            int mineDepth = ActiveMine.rolledDepth;
            if (gy < LobbyFloorY || gy >= LobbyFloorY + mineDepth)
                return false;

            if (!IsInsideWellArea(gx, gz) && !IsOnWellRimSurface(gx, gy, gz))
                return false;

            if (!lockDeeperLayersUntilCleared)
                return true;

            if (!IsInsideWellArea(gx, gz))
                return true;

            if (gy <= LobbyFloorY) // Самый верхний слой шахты
                return true;

            return IsWellLayerCleared(gy - 1);
        }

        public int GetContiguousClearedDepth()
        {
            if (ActiveMine == null) return 0;

            int cleared = 0;
            for (int y = 0; y < ActiveMine.rolledDepth; y++)
            {
                if (!IsWellLayerCleared(LobbyFloorY + y))
                    break;
                cleared++;
            }
            return cleared;
        }

        public void GeneratePlotExtension(int offsetX, int offsetZ, int width, int length)
        {
            Debug.Log($"[WellGenerator] Покупка участка +[{offsetX},{offsetZ}] size {width}x{length}");
        }

        private void SpawnPlayerOnGround(int surfaceY = 0)
        {
            Transform player = ResolveOrSpawnPlayer();
            if (player == null)
            {
                Debug.LogWarning("[WellGenerator] Игрок не найден и playerPrefab не задан.");
                return;
            }

            if (!TryFindGroundSpawnCell(surfaceY, out int gx, out int gz))
            {
                Debug.LogWarning("[WellGenerator] Не удалось найти клетку земли для спавна игрока.");
                return;
            }

            Vector3 baseWorldPos = island.transform.TransformPoint(island.GridToLocal(gx, surfaceY, gz) + new Vector3(0.5f, 0f, 0.5f));
            float groundY = GetGroundYAt(baseWorldPos.x, baseWorldPos.z);
            float spawnY = ComputePlayerSpawnY(player, groundY);
            Vector3 spawnPos = new Vector3(baseWorldPos.x, spawnY, baseWorldPos.z);

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            player.position = spawnPos;

            int centerX = padding + wellWidth / 2;
            int centerZ = padding + wellLength / 2;
            Vector3 lookTarget = island.GridToLocal(centerX, 0, centerZ) + new Vector3(0.5f, 0f, 0.5f);
            Vector3 lookDir = lookTarget - player.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
                player.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

            if (cc != null)
                cc.enabled = true;
        }

        void SpawnPlayerAt(Vector3 pos)
        {
            Transform player = ResolveOrSpawnPlayer();
            if (player == null)
            {
                Debug.LogWarning("[WellGenerator] Игрок не найден и playerPrefab не задан.");
                return;
            }

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            // Adjust Y based on player's collider height
            float spawnY = ComputePlayerSpawnY(player, pos.y);
            player.position = new Vector3(pos.x, spawnY, pos.z);

            // Look towards the center of the mine
            if (ActiveMine != null)
            {
                Vector3 mineCenterWorld = island.GridToLocal(ActiveMine.originX, LobbyFloorY, ActiveMine.originZ);
                Vector3 lookDir = mineCenterWorld - player.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.0001f)
                    player.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            if (cc != null)
                cc.enabled = true;
        }

        private float GetGroundYAt(float worldX, float worldZ)
        {
            MeshCollider islandCollider = island.GetComponent<MeshCollider>();
            Vector3 rayOrigin = new Vector3(worldX, island.transform.position.y + island.TotalY + 10f, worldZ); // Ray from above the whole island
            Ray ray = new Ray(rayOrigin, Vector3.down);

            if (islandCollider != null && islandCollider.Raycast(ray, out RaycastHit hit, island.TotalY + 30f))
                return hit.point.y;

            return island.transform.position.y; // Fallback
        }

        private float ComputePlayerSpawnY(Transform player, float groundY)
        {
            const float clearance = 0.03f;

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                float bottomOffset = cc.center.y - (cc.height * 0.5f);
                return groundY + clearance - bottomOffset;
            }

            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                float bottomOffset = capsule.center.y - (capsule.height * 0.5f);
                return groundY + clearance - bottomOffset;
            }

            Collider col = player.GetComponent<Collider>();
            if (col != null)
            {
                float bottomOffset = col.bounds.min.y - player.position.y;
                return groundY + clearance - bottomOffset;
            }

            return groundY + playerSpawnHeight;
        }

        private Transform ResolveOrSpawnPlayer()
        {
            if (playerToPlace != null)
                return playerToPlace;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour b = behaviours[i];
                if (b != null && b.GetType().Name == "PlayerCharacterController")
                    return b.transform;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                return taggedPlayer.transform;

            if (playerPrefab == null)
                return null;

            GameObject spawned = Instantiate(playerPrefab);
            if (spawned.tag != "Player")
                spawned.tag = "Player";
            return spawned.transform;
        }

        private bool TryFindGroundSpawnCell(int surfaceY, out int gx, out int gz)
        {
            // Try to find a spot near the center of the lobby area
            int centerX = island.TotalX / 2;
            int centerZ = island.TotalZ / 2;

            // Check center first
            if (island.IsInBounds(centerX, surfaceY, centerZ) && island.IsSolid(centerX, surfaceY, centerZ))
            {
                gx = centerX;
                gz = centerZ;
                return true;
            }

            // Expand search outwards
            for (int radius = 1; radius < Mathf.Max(island.TotalX, island.TotalZ) / 2; radius++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    for (int z = centerZ - radius; z <= centerZ + radius; z++)
                    {
                        if (island.IsInBounds(x, surfaceY, z) && island.IsSolid(x, surfaceY, z))
                        {
                            gx = x;
                            gz = z;
                            return true;
                        }
                    }
                }
            }

            // Fallback: check every cell
            for (int x = 0; x < island.TotalX; x++)
            for (int z = 0; z < island.TotalZ; z++)
            {
                if (island.IsSolid(x, surfaceY, z))
                {
                    gx = x;
                    gz = z;
                    return true;
                }
            }

            gx = 0;
            gz = 0;
            return false;
        }

        void CreateElevator(int gx, int gz)
        {
            // Удаляем старые лифты
            foreach (var old in GetComponentsInChildren<SimpleElevator>())
                Destroy(old.gameObject);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "AutoElevator";
            go.transform.SetParent(this.transform);
            
            // Ставим лифт на край шахты
            // gx, gz - это x0, z0 из GenerateMineAt, т.е. левый-нижний угол шахты с паддингом
            // Лифт ставим на 1 блок левее/севернее от этого угла
            Vector3 localPos = island.GridToLocal(gx, LobbyFloorY, gz);
            go.transform.position = island.transform.TransformPoint(localPos + new Vector3(0.5f, 0.125f, 0.5f));
            go.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);

            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 2f, 1f);
            trigger.center = new Vector3(0f, 1f, 0f);

            SimpleElevator elevatorScript = go.AddComponent<SimpleElevator>();
            elevatorScript.topY = go.transform.position.y;
            elevatorScript.wellGenerator = this;
            elevatorScript.island = island;
            elevatorScript.shaftGridX = gx; 
            elevatorScript.shaftGridZ = gz;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = elevatorColor;
                renderer.material = material;
            }
        }

        private bool IsWellLayerCleared(int depthGridY)
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
                if (island.IsInBounds(x, depthGridY, z) && island.IsSolid(x, depthGridY, z))
                    return false;
            }

            return true;
        }

        private BlockType DetermineBlockType(int depthIndex)
        {
            if (depthIndex == 0)
                return BlockType.Dirt;

            float rand = UnityEngine.Random.value;
            if (depthIndex < TopLayerDepth)
                return rand < 0.9f ? BlockType.Dirt : BlockType.Stone;

            if (depthIndex < MidLayerDepth)
            {
                if (rand < 0.5f) return BlockType.Stone;
                if (rand < 0.8f) return BlockType.Dirt;
                return BlockType.Iron;
            }

            return rand < 0.8f ? BlockType.Stone : BlockType.Gold;
        }

        private void SyncColorsToIsland()
        {
            int typeCount = System.Enum.GetValues(typeof(BlockType)).Length;

            Color[] cols = new Color[typeCount];
            for (int i = 0; i < typeCount && i < island.blockColors.Length; i++)
                cols[i] = island.blockColors[i];

            foreach (BlockData bd in blockDataConfig)
            {
                int idx = (int)bd.type;
                if (idx < typeCount && bd.blockColor.a > 0.01f)
                    cols[idx] = bd.blockColor;
            }

            island.blockColors = cols;
        }

        private void EnsureBasicBlockConfig()
        {
            if (blockDataConfig == null) blockDataConfig = new List<BlockData>();
            
            // Проверяем наличие Grass и Dirt в конфиге (если их нет в инспекторе)
            bool hasGrass = false;
            bool hasDirt = false;
            foreach (var b in blockDataConfig)
            {
                if (b.type == BlockType.Grass) hasGrass = true;
                if (b.type == BlockType.Dirt) hasDirt = true;
            }

            if (!hasGrass) blockDataConfig.Add(new BlockData { type = BlockType.Grass, blockColor = new Color(0.2f, 0.8f, 0.2f, 1f), maxHealth = 3 });
            if (!hasDirt) blockDataConfig.Add(new BlockData { type = BlockType.Dirt, blockColor = new Color(0.5f, 0.3f, 0.1f, 1f), maxHealth = 3 });
            
            SyncColorsToIsland();
        }
    }
}
