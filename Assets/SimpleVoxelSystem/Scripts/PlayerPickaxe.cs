using System.Collections.Generic;
using UnityEngine;
using SimpleVoxelSystem.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Кирка игрока. Raycast попадает на единый меш VoxelIsland,
    /// вычисляет воксельные координаты из точки удара и добывает блок.
    /// </summary>
    public class PlayerPickaxe : MonoBehaviour
    {
        [Header("Mining Setup")]
        public int pickaxePower = 1;
        public float miningRange = 100f;
        public Camera miningCamera;
        public LayerMask miningLayers = Physics.DefaultRaycastLayers;
        public WellGenerator wellGenerator;
        public bool verboseLogs = false;
        public bool enableManualRaycastMining = true;

        [Header("Inventory")]
        public int currentBackpackLoad = 0;
        public int maxBackpackCapacity = 10;
        public int totalValueInBackpack = 0;

        [Header("Resource Counts")]
        public int dirtCount = 0;
        public int stoneCount = 0;
        public int ironCount = 0;
        public int goldCount = 0;

        private readonly Dictionary<Vector3Int, int> blockHealth = new Dictionary<Vector3Int, int>();
        private readonly Dictionary<BlockType, BlockData> dataCache = new Dictionary<BlockType, BlockData>();
        private static readonly BlockData FallbackData = new BlockData { maxHealth = 1, reward = 1, type = BlockType.Dirt, blockColor = Color.white };

        void Awake()
        {
            if (miningCamera == null)
                miningCamera = Camera.main;

            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();

            RebuildDataCache();
        }

        void Update()
        {
            if (!enableManualRaycastMining)
                return;

            if (!IsMinePressedDown())
                return;

            if (currentBackpackLoad >= maxBackpackCapacity)
            {
                Debug.Log("Рюкзак полон! Нужно разгрузиться на складе.");
                return;
            }

            MineRaycast();
        }

        public bool TryMineGridTarget(int gx, int gy, int gz, VoxelIsland islandOverride = null)
        {
            if (currentBackpackLoad >= maxBackpackCapacity)
            {
                Debug.Log("Рюкзак полон! Нужно разгрузиться на складе.");
                return false;
            }

            VoxelIsland island = islandOverride;
            if (island == null)
            {
                if (wellGenerator != null)
                    island = wellGenerator.GetComponent<VoxelIsland>();

                if (island == null)
                    island = FindFirstObjectByType<VoxelIsland>();
            }

            if (island == null)
                return false;

            if (!island.TryGetBlockType(gx, gy, gz, out BlockType blockType))
            {
                blockHealth.Remove(new Vector3Int(gx, gy, gz));
                return false;
            }

            if (wellGenerator != null && !wellGenerator.CanMineVoxel(gx, gy, gz))
            {
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] Сначала очистите предыдущий слой. Заблокирована глубина y={gy}.", this);
                return false;
            }

            if (wellGenerator == null)
            {
                wellGenerator = island.GetComponent<WellGenerator>();
                RebuildDataCache();
            }

            BlockData data = GetBlockData(blockType);
            MineBlock(gx, gy, gz, data, island);
            return true;
        }

        void MineRaycast()
        {
            if (miningCamera == null)
            {
                miningCamera = Camera.main;
                if (miningCamera == null)
                {
                    Debug.LogWarning("[Pickaxe] Не найдена камера для Raycast (miningCamera/Camera.main).", this);
                    return;
                }
            }

            Vector2 pointerPos = ReadPointerPosition();
            Ray ray = miningCamera.ScreenPointToRay(pointerPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, miningRange, miningLayers, QueryTriggerInteraction.Ignore))
            {
                if (verboseLogs)
                    Debug.Log("[Pickaxe] Raycast не попал в коллайдер в пределах дистанции.", this);
                return;
            }

            VoxelIsland island = hit.collider.GetComponentInParent<VoxelIsland>();
            if (island == null)
            {
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] Попадание в {hit.collider.name}, но это не VoxelIsland.", this);
                return;
            }

            if (wellGenerator == null)
            {
                wellGenerator = island.GetComponent<WellGenerator>();
                RebuildDataCache();
            }

            Vector3 localHit = island.transform.InverseTransformPoint(hit.point - hit.normal * 0.5f);

            int gx = Mathf.FloorToInt(localHit.x);
            int gy = -Mathf.FloorToInt(localHit.y);
            int gz = Mathf.FloorToInt(localHit.z);

            if (!island.TryGetBlockType(gx, gy, gz, out BlockType blockType))
            {
                blockHealth.Remove(new Vector3Int(gx, gy, gz));
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] Промах — в [{gx},{gy},{gz}] нет блока.", this);
                return;
            }

            if (wellGenerator != null && !wellGenerator.CanMineVoxel(gx, gy, gz))
            {
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] Копать запрещено в [{gx},{gy},{gz}] (нет шахты или заблокирован слой).", this);
                return;
            }

            BlockData data = GetBlockData(blockType);
            MineBlock(gx, gy, gz, data, island);
        }

        void MineBlock(int gx, int gy, int gz, BlockData data, VoxelIsland island)
        {
            Vector3Int key = new Vector3Int(gx, gy, gz);
            int currentHealth = GetOrCreateBlockHealth(key, data);
            int damage = Mathf.Max(1, pickaxePower);
            currentHealth -= damage;

            if (currentHealth > 0)
            {
                blockHealth[key] = currentHealth;
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] {data.type} [{gx},{gy},{gz}] HP: {currentHealth}", this);
                return;
            }

            blockHealth.Remove(key);
            CollectResources(data);

            if (wellGenerator != null)
                wellGenerator.MineVoxel(gx, gy, gz);
            else
                island.RemoveVoxel(gx, gy, gz);
        }

        int GetOrCreateBlockHealth(Vector3Int key, BlockData data)
        {
            if (blockHealth.TryGetValue(key, out int hp))
                return hp;

            return Mathf.Max(1, data.maxHealth);
        }

        BlockData GetBlockData(BlockType type)
        {
            if (dataCache.Count == 0)
                RebuildDataCache();

            if (dataCache.TryGetValue(type, out BlockData data) && data != null)
                return data;

            FallbackData.type = type;
            return FallbackData;
        }

        void RebuildDataCache()
        {
            dataCache.Clear();
            if (wellGenerator == null || wellGenerator.blockDataConfig == null)
                return;

            foreach (BlockData data in wellGenerator.blockDataConfig)
            {
                if (data != null)
                    dataCache[data.type] = data;
            }
        }

        bool IsMinePressedDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        Vector2 ReadPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        void CollectResources(BlockData data)
        {
            currentBackpackLoad++;
            totalValueInBackpack += data.reward;

            switch (data.type)
            {
                case BlockType.Dirt: dirtCount++; break;
                case BlockType.Stone: stoneCount++; break;
                case BlockType.Iron: ironCount++; break;
                case BlockType.Gold: goldCount++; break;
            }

            Debug.Log(
                $"Добыт: {data.type} (+{data.reward}₽). " +
                $"Рюкзак: {currentBackpackLoad}/{maxBackpackCapacity}. " +
                $"[D:{dirtCount} S:{stoneCount} Fe:{ironCount} Au:{goldCount}]"
            );
        }

        public void SellResources()
        {
            if (currentBackpackLoad <= 0)
                return;

            GlobalEconomy.Money += totalValueInBackpack;
            Debug.Log($"Продано на {totalValueInBackpack}₽. Итого: {GlobalEconomy.Money}₽");
            ClearBackpack();
        }

        public void ClearBackpack()
        {
            currentBackpackLoad = 0;
            totalValueInBackpack = 0;
            dirtCount = stoneCount = ironCount = goldCount = 0;
        }
    }
}
