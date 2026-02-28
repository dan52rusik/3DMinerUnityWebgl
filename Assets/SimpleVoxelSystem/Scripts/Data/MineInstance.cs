using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Данные об активной (купленной, но ещё не истощённой) шахте.
    /// Создаётся при покупке, уничтожается при продаже/истощении.
    /// </summary>
    public class MineInstance
    {
        public MineShopData shopData;       // ScriptableObject с настройками
        public int          rolledDepth;    // Реальная глубина (выпала при покупке)
        public int          totalBlocks;    // Сколько блоков было при генерации
        public int          minedBlocks;    // Сколько уже добыто

        public System.Collections.Generic.HashSet<Vector3Int> minedPositions = new System.Collections.Generic.HashSet<Vector3Int>();

        public int originX, originZ; // координаты размещения на острове
        public bool IsExhausted => minedBlocks >= totalBlocks;

        /// <summary>Цена продажи истощённой шахты.</summary>
        public int SellPrice => Mathf.RoundToInt(shopData.buyPrice * shopData.sellBackRatio);

        public MineInstance(MineShopData data, int depth, int totalBlockCount)
        {
            shopData    = data;
            rolledDepth = depth;
            totalBlocks = totalBlockCount;
            minedBlocks = 0;
        }

        public void RegisterMinedBlock(int x, int y, int z)
        {
            // Сохраняем локальные координаты (относительно начала шахты)
            if (minedPositions.Add(new Vector3Int(x - originX, y, z - originZ)))
                minedBlocks++;
        }

        public bool IsVoxelMined(int x, int y, int z) => minedPositions.Contains(new Vector3Int(x - originX, y, z - originZ));
    }
}
