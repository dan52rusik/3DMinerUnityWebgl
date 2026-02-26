using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Кирка игрока. Raycast попадает на единый меш VoxelIsland,
    /// вычисляет воксельные координаты из точки удара и сообщает WellGenerator.
    /// </summary>
    public class PlayerPickaxe : MonoBehaviour
    {
        [Header("Mining Setup")]
        public int   pickaxePower  = 1;
        public float miningRange   = 5f;
        public WellGenerator wellGenerator;

        [Header("Inventory")]
        public int currentBackpackLoad = 0;
        public int maxBackpackCapacity = 10;
        public int totalValueInBackpack = 0;

        [Header("Resource Counts")]
        public int dirtCount  = 0;
        public int stoneCount = 0;
        public int ironCount  = 0;
        public int goldCount  = 0;

        // ──────────────────────────────────────────────────────────────────────

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (currentBackpackLoad < maxBackpackCapacity)
                    MineRaycast();
                else
                    Debug.Log("Рюкзак полон! Нужно разгрузиться на складе.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────

        void MineRaycast()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, miningRange)) return;

            // Убеждаемся, что попали в объект с VoxelIsland
            VoxelIsland island = hit.collider.GetComponent<VoxelIsland>();
            if (island == null) return;

            // Вычисляем воксельные индексы из мировой точки удара.
            // Слегка «заходим» внутрь блока по нормали, чтобы не попасть на границу.
            Vector3 localHit = island.transform.InverseTransformPoint(hit.point - hit.normal * 0.5f);

            // В VoxelIsland: ось Y инвертирована (глубина = -y)
            int gx = Mathf.FloorToInt(localHit.x);
            int gy = Mathf.FloorToInt(-localHit.y);   // инверсия
            int gz = Mathf.FloorToInt(localHit.z);

            // Запрашиваем тип блока
            if (!island.TryGetBlockType(gx, gy, gz, out BlockType blockType))
            {
                Debug.Log($"[Pickaxe] Промах — в [{gx},{gy},{gz}] нет блока.");
                return;
            }

            // Получаем данные блока из конфига генератора
            if (wellGenerator == null) return;
            BlockData data = wellGenerator.blockDataConfig.Find(d => d.type == blockType);
            if (data == null) return;

            // Наносим урон (health хранится снаружи; для простоты — one-hit через pickaxePower)
            // Для multi-hit нужен отдельный словарь: Dictionary<Vector3Int, int> blockHealth
            MineBlock(gx, gy, gz, data, island);
        }

        void MineBlock(int gx, int gy, int gz, BlockData data, VoxelIsland island)
        {
            // Простейшая логика: один удар = блок сломан (для полноценного multi-hit
            // добавить Dictionary<Vector3Int, int> с текущим здоровьем)
            CollectResources(data);
            wellGenerator.MineVoxel(gx, gy, gz);
        }

        // ──────────────────────────────────────────────────────────────────────

        void CollectResources(BlockData data)
        {
            currentBackpackLoad++;
            totalValueInBackpack += data.reward;

            switch (data.type)
            {
                case BlockType.Dirt:  dirtCount++;  break;
                case BlockType.Stone: stoneCount++; break;
                case BlockType.Iron:  ironCount++;  break;
                case BlockType.Gold:  goldCount++;  break;
            }

            Debug.Log(
                $"Добыт: {data.type} (+{data.reward}₽). " +
                $"Рюкзак: {currentBackpackLoad}/{maxBackpackCapacity}. " +
                $"[D:{dirtCount} S:{stoneCount} Fe:{ironCount} Au:{goldCount}]"
            );
        }

        // ──────────────────────────────────────────────────────────────────────

        public void SellResources()
        {
            if (currentBackpackLoad > 0)
            {
                GlobalEconomy.Money += totalValueInBackpack;
                Debug.Log($"Продано на {totalValueInBackpack}₽. Итого: {GlobalEconomy.Money}₽");
                ClearBackpack();
            }
        }

        public void ClearBackpack()
        {
            currentBackpackLoad  = 0;
            totalValueInBackpack = 0;
            dirtCount = stoneCount = ironCount = goldCount = 0;
        }
    }
}
