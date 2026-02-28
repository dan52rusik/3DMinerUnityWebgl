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

        public void RegisterMinedBlock() => minedBlocks++;
    }
}
