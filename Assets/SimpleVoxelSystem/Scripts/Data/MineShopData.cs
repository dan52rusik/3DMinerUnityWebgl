using System;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// ScriptableObject — описание одного «класса» шахты в магазине.
    /// Создать: Assets → Create → SimpleVoxelSystem → Mine Shop Data
    /// </summary>
    [CreateAssetMenu(
        menuName = "SimpleVoxelSystem/Mine Shop Data",
        fileName = "MineShopData_New")]
    public class MineShopData : ScriptableObject
    {
        [Header("Внешний вид в магазине")]
        public string displayName  = "Бронзовая шахта";
        [TextArea(2, 4)]
        public string description  = "Небольшая шахта с преимущественно каменными породами.";
        public Color  labelColor   = new Color(0.8f, 0.5f, 0.2f);

        [Header("Цена")]
        public int buyPrice  = 500;   // цена покупки
        [Range(0f, 1f)]
        public float sellBackRatio = 0.5f; // за сколько можно продать истощённую шахту

        [Header("Размер шахты")]
        public int wellWidth  = 5;
        public int wellLength = 5;
        [Range(1, 30)]
        public int depthMin = 3;
        [Range(1, 30)]
        public int depthMax = 6;
        public int padding  = 3;

        [Header("Состав блоков (веса по слою)")]
        public BlockLayer[] layers = new BlockLayer[]
        {
            new BlockLayer { maxDepth = 2,  dirtWeight = 90, stoneWeight = 10, ironWeight = 0,  goldWeight = 0  },
            new BlockLayer { maxDepth = 10, dirtWeight = 30, stoneWeight = 55, ironWeight = 15, goldWeight = 0  },
            new BlockLayer { maxDepth = 30, dirtWeight = 10, stoneWeight = 60, ironWeight = 25, goldWeight = 5  },
        };

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Возвращает случайную глубину в допустимом диапазоне.</summary>
        public int RollDepth() => UnityEngine.Random.Range(depthMin, depthMax + 1);

        /// <summary>Возвращает случайный тип блока для заданной глубины.</summary>
        public BlockType RollBlockType(int depth)
        {
            // Ищем подходящий слой
            BlockLayer layer = layers[layers.Length - 1];
            for (int i = 0; i < layers.Length; i++)
            {
                if (depth <= layers[i].maxDepth)
                {
                    layer = layers[i];
                    break;
                }
            }

            int total = layer.dirtWeight + layer.stoneWeight + layer.ironWeight + layer.goldWeight;
            if (total <= 0) return BlockType.Stone;

            int roll = UnityEngine.Random.Range(0, total);
            if (roll < layer.dirtWeight)  return BlockType.Dirt;
            roll -= layer.dirtWeight;
            if (roll < layer.stoneWeight) return BlockType.Stone;
            roll -= layer.stoneWeight;
            if (roll < layer.ironWeight)  return BlockType.Iron;
            return BlockType.Gold;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class BlockLayer
    {
        [Tooltip("Эти веса применяются для глубин 0..maxDepth")]
        public int maxDepth = 5;

        [Header("Веса (сумма произвольная, нормализуется автоматически)")]
        public int dirtWeight  = 50;
        public int stoneWeight = 40;
        public int ironWeight  = 10;
        public int goldWeight  = 0;
    }
}
