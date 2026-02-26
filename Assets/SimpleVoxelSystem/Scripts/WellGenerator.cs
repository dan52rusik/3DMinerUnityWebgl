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

        [Header("Блоки")]
        public List<BlockData> blockDataConfig;

        [Header("Mining Rules")]
        public bool lockDeeperLayersUntilCleared = true;

        [Header("Elevator")]
        public bool createElevatorOnStart = true;
        public Color elevatorColor = new Color(0.6f, 0.3f, 0.1f, 1f);

        [Header("Player Spawn")]
        public bool spawnPlayerOnStart = true;
        public GameObject playerPrefab;
        public Transform playerToPlace;
        public float playerSpawnHeight = 1.05f;

        private const int TopLayerDepth = 3;
        private const int MidLayerDepth = 7;

        private VoxelIsland island;

        void Start()
        {
            if (blockDataConfig == null || blockDataConfig.Count == 0)
            {
                Debug.LogWarning("[WellGenerator] Нет blockDataConfig в инспекторе!");
                return;
            }

            island = GetComponent<VoxelIsland>();
            SyncColorsToIsland();

            island.Init(wellWidth, wellDepth, wellLength, padding, padding);
            GenerateStartIsland();
        }

        void GenerateStartIsland()
        {
            for (int x = 0; x < island.TotalX; x++)
            for (int z = 0; z < island.TotalZ; z++)
            for (int y = 0; y < wellDepth; y++)
            {
                bool inWell = IsInsideWellArea(x, z);
                BlockType t = inWell ? DetermineBlockType(y) : BlockType.Dirt;
                island.SetVoxel(x, y, z, t);
            }

            island.RebuildMesh();

            if (createElevatorOnStart)
                CreateElevator(padding, padding);

            if (spawnPlayerOnStart)
                SpawnPlayerOnGround();

            Debug.Log($"[WellGenerator] Остров построен. Статистика: {island.TotalX}x{island.TotalY}x{island.TotalZ} вокселей → 1 draw call.");
        }

        public void MineVoxel(int gx, int gy, int gz)
        {
            island.RemoveVoxel(gx, gy, gz);
        }

        public bool CanMineVoxel(int gx, int gy, int gz)
        {
            if (gy < 0 || gy >= wellDepth)
                return false;

            if (!IsInsideWellArea(gx, gz) && !IsOnWellRimSurface(gx, gy, gz))
                return false;

            if (!lockDeeperLayersUntilCleared)
                return true;

            if (!IsInsideWellArea(gx, gz))
                return true;

            if (gy <= 0)
                return true;

            return IsWellLayerCleared(gy - 1);
        }

        public int GetContiguousClearedDepth()
        {
            int cleared = 0;
            for (int y = 0; y < wellDepth; y++)
            {
                if (!IsWellLayerCleared(y))
                    break;
                cleared++;
            }
            return cleared;
        }

        public void GeneratePlotExtension(int offsetX, int offsetZ, int width, int length)
        {
            Debug.Log($"[WellGenerator] Покупка участка +[{offsetX},{offsetZ}] size {width}x{length}");
        }

        private void SpawnPlayerOnGround()
        {
            Transform player = ResolveOrSpawnPlayer();
            if (player == null)
            {
                Debug.LogWarning("[WellGenerator] Игрок не найден и playerPrefab не задан.");
                return;
            }

            if (!TryFindGroundSpawnCell(out int gx, out int gz))
            {
                Debug.LogWarning("[WellGenerator] Не удалось найти клетку земли для спавна игрока.");
                return;
            }

            Vector3 baseWorldPos = island.transform.TransformPoint(island.GridToLocal(gx, 0, gz) + new Vector3(0.5f, 0f, 0.5f));
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

        private float GetGroundYAt(float worldX, float worldZ)
        {
            MeshCollider islandCollider = island.GetComponent<MeshCollider>();
            Vector3 rayOrigin = new Vector3(worldX, island.transform.position.y + wellDepth + 10f, worldZ);
            Ray ray = new Ray(rayOrigin, Vector3.down);

            if (islandCollider != null && islandCollider.Raycast(ray, out RaycastHit hit, wellDepth + 30f))
                return hit.point.y;

            return island.transform.position.y;
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

        private bool TryFindGroundSpawnCell(out int gx, out int gz)
        {
            int centerX = padding + wellWidth / 2;
            int centerZ = padding + wellLength / 2;

            Vector2Int[] preferred = new Vector2Int[]
            {
                new Vector2Int(centerX, padding - 1),
                new Vector2Int(centerX, padding + wellLength),
                new Vector2Int(padding - 1, centerZ),
                new Vector2Int(padding + wellWidth, centerZ),
                new Vector2Int(padding - 1, padding - 1),
                new Vector2Int(padding + wellWidth, padding + wellLength),
                new Vector2Int(padding - 1, padding + wellLength),
                new Vector2Int(padding + wellWidth, padding - 1),
            };

            for (int i = 0; i < preferred.Length; i++)
            {
                int x = preferred[i].x;
                int z = preferred[i].y;
                if (x < 0 || x >= island.TotalX || z < 0 || z >= island.TotalZ)
                    continue;
                if (island.IsSolid(x, 0, z))
                {
                    gx = x;
                    gz = z;
                    return true;
                }
            }

            for (int x = 0; x < island.TotalX; x++)
            for (int z = 0; z < island.TotalZ; z++)
            {
                if (island.IsSolid(x, 0, z))
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

        private void CreateElevator(int gridX, int gridZ)
        {
            if (gridX < 0 || gridX >= island.TotalX || gridZ < 0 || gridZ >= island.TotalZ)
            {
                Debug.LogWarning($"[WellGenerator] Elevator coords out of bounds: [{gridX},{gridZ}]");
                return;
            }

            for (int y = 0; y < wellDepth; y++)
                island.RemoveVoxel(gridX, y, gridZ, false);

            island.RebuildMesh();

            GameObject elevatorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            elevatorObj.name = "AutoElevator";
            elevatorObj.transform.SetParent(transform, true);
            elevatorObj.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);
            elevatorObj.transform.position = island.GridToLocal(gridX, 0, gridZ) + new Vector3(0.5f, 0.125f, 0.5f);

            BoxCollider trigger = elevatorObj.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 2f, 1f);
            trigger.center = new Vector3(0f, 1f, 0f);

            SimpleElevator elevatorScript = elevatorObj.AddComponent<SimpleElevator>();
            elevatorScript.topY = elevatorObj.transform.position.y;
            elevatorScript.wellGenerator = this;
            elevatorScript.island = island;
            elevatorScript.shaftGridX = gridX;
            elevatorScript.shaftGridZ = gridZ;

            MeshRenderer renderer = elevatorObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = elevatorColor;
                renderer.material = material;
            }
        }

        private bool IsInsideWellArea(int gx, int gz)
        {
            return gx >= padding && gx < padding + wellWidth &&
                   gz >= padding && gz < padding + wellLength;
        }

        private bool IsOnWellRimSurface(int gx, int gy, int gz)
        {
            if (gy != 0)
                return false;

            int minX = padding - 1;
            int maxX = padding + wellWidth;
            int minZ = padding - 1;
            int maxZ = padding + wellLength;

            bool inExpanded = gx >= minX && gx <= maxX && gz >= minZ && gz <= maxZ;
            return inExpanded && !IsInsideWellArea(gx, gz);
        }

        private bool IsWellLayerCleared(int depthIndex)
        {
            if (depthIndex < 0)
                return true;
            if (depthIndex >= wellDepth)
                return false;

            for (int x = padding; x < padding + wellWidth; x++)
            for (int z = padding; z < padding + wellLength; z++)
            {
                if (island.IsSolid(x, depthIndex, z))
                    return false;
            }

            return true;
        }

        private BlockType DetermineBlockType(int depthIndex)
        {
            if (depthIndex == 0)
                return BlockType.Dirt;

            float rand = Random.value;
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

            for (int i = 0; i < cols.Length; i++)
                Debug.Log($"[WellGenerator] BlockColor[{(BlockType)i}] = {cols[i]}");
        }
    }
}
