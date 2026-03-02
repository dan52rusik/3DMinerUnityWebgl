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
        [Min(0.1f)] public float backpackFullLogCooldown = 1.5f;

        [Header("Pickaxe Evolution")]
        public PickaxeData currentPickaxe;


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
        private float nextBackpackLogTime;

        private Net.NetPlayerAvatar networkAvatar;

        void Awake()
        {
            if (miningCamera == null)
                miningCamera = Camera.main;

            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();

            networkAvatar = GetComponent<Net.NetPlayerAvatar>();

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
                LogBackpackFull();
                return;
            }

            MineRaycast();
        }

        public bool TryMineGridTarget(int gx, int gy, int gz, VoxelIsland islandOverride = null)
        {
            if (currentBackpackLoad >= maxBackpackCapacity)
            {
                LogBackpackFull();
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

            if (blockType == BlockType.Air)
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

            // Проверка уровня копки
            if (GlobalEconomy.MiningLevel < data.requiredMiningLevel)
            {
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] Нужен уровень копки {data.requiredMiningLevel}, чтобы добывать {data.type}!");
                return false;
            }

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

            if (blockType == BlockType.Air)
            {
                blockHealth.Remove(new Vector3Int(gx, gy, gz));
                if (verboseLogs)
                    Debug.Log($"[Pickaxe] Промах — в [{gx},{gy},{gz}] воздух.", this);
                return;
            }

            if (wellGenerator != null && !wellGenerator.CanMineVoxel(gx, gy, gz))
            {
                // Показываем причину в консоль раз в 1 сек, чтобы пользователь видел блокировку
                if (Time.frameCount % 90 == 0)
                    Debug.Log($"[Pickaxe] Не могу копать в [{gx},{gy},{gz}]. Проверьте глубину вашей шахты!");
                return;
            }

            BlockData data = GetBlockData(blockType);

            // Проверка уровня копки
            if (GlobalEconomy.MiningLevel < data.requiredMiningLevel)
            {
                if (Time.frameCount % 90 == 0)
                    Debug.Log($"<color=orange>[Pickaxe] Нужен уровень копки {data.requiredMiningLevel}, чтобы добывать {data.type}!</color>");
                return;
            }

            MineBlock(gx, gy, gz, data, island);
        }

        void MineBlock(int gx, int gy, int gz, BlockData data, VoxelIsland island)
        {
            Vector3Int key = new Vector3Int(gx, gy, gz);
            int currentHealth = GetOrCreateBlockHealth(key, data);
            
            int power = (currentPickaxe != null) ? currentPickaxe.miningPower : pickaxePower;
            int damage = Mathf.Max(1, power);
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
            
            // Начисление опыта
            int xp = data.xpReward;
            if (xp <= 0) xp = 2; // Минимальный XP
            bool inLobby = wellGenerator != null && wellGenerator.IsInLobbyMode;

            // Локальный апдейт острова (Client-side prediction)
            if (wellGenerator != null)
                wellGenerator.MineVoxel(gx, gy, gz);
            else
                island.RemoveVoxel(gx, gy, gz);

            AsyncGameplayEvents.PublishMineBlock(gx, gy, gz, data.type, xp, inLobby);

            // СИНХРОНИЗАЦИЯ: Отправляем на сервер
            if (networkAvatar != null && networkAvatar.IsSpawned)
            {
                networkAvatar.RequestMineBlockServerRpc(new Vector3Int(gx, gy, gz), inLobby);
                networkAvatar.AddRewardsServerRpc(0, xp); // Деньги при продаже, XP сейчас
            }
            else
            {
                // Если не в сети — просто локально
                GlobalEconomy.AddMiningXP(xp);
            }
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

        public void RebuildDataCache()
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

            if (verboseLogs)
            {
                Debug.Log(
                    $"Добыт: {data.type} (+{data.reward}₽). " +
                    $"Рюкзак: {currentBackpackLoad}/{maxBackpackCapacity}. " +
                    $"[D:{dirtCount} S:{stoneCount} Fe:{ironCount} Au:{goldCount}]"
                );
            }
        }

        public void SellResources()
        {
            if (currentBackpackLoad <= 0)
                return;

            if (networkAvatar != null && networkAvatar.IsSpawned)
            {
                networkAvatar.AddRewardsServerRpc(totalValueInBackpack, 0);
            }
            else
            {
                GlobalEconomy.Money += totalValueInBackpack;
            }

            AsyncGameplayEvents.PublishSellBackpack(totalValueInBackpack);

            Debug.Log($"Продано на {totalValueInBackpack}₽. Итого: {GlobalEconomy.Money}₽");
            ClearBackpack();
        }

        public void ClearBackpack()
        {
            currentBackpackLoad = 0;
            totalValueInBackpack = 0;
            dirtCount = stoneCount = ironCount = goldCount = 0;
        }

        void LogBackpackFull()
        {
            if (Time.unscaledTime < nextBackpackLogTime)
                return;

            nextBackpackLogTime = Time.unscaledTime + backpackFullLogCooldown;
            Debug.Log("Рюкзак полон! Нужно разгрузиться на складе.");
        }
    }
}
