using System;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// ScriptableObject — description of one "class" of mine in the shop.
    /// Create: Assets → Create → SimpleVoxelSystem → Mine Shop Data
    /// </summary>
    [CreateAssetMenu(
        menuName = "SimpleVoxelSystem/Mine Shop Data",
        fileName = "MineShopData_New")]
    public class MineShopData : ScriptableObject
    {
        [Header("Shop Appearance")]
        public string displayName  = "Bronze Mine";
        [TextArea(2, 4)]
        public string description  = "A small mine with predominantly stone rocks.";
        public Color  labelColor   = new Color(0.8f, 0.5f, 0.2f);

        [Header("Price")]
        public int buyPrice  = EconomyTuning.BronzeMinePrice;   // purchase price
        [Range(0f, 1f)]
        public float sellBackRatio = EconomyTuning.BronzeMineSellBackRatio; // sale price of exhausted mine

        [Header("Mine Size")]
        public int wellWidth  = EconomyTuning.DefaultMineWellWidth;
        public int wellLength = EconomyTuning.DefaultMineWellLength;
        [Range(1, 30)]
        public int depthMin = EconomyTuning.BronzeMineDepthMin;
        [Range(1, 30)]
        public int depthMax = EconomyTuning.BronzeMineDepthMax;
        public int padding  = EconomyTuning.DefaultMinePadding;

        [Header("Block Composition (weights by layer)")]
        public BlockLayer[] layers = new BlockLayer[]
        {
            new BlockLayer { maxDepth = 2,  dirtWeight = 90, stoneWeight = 10, ironWeight = 0,  goldWeight = 0  },
            new BlockLayer { maxDepth = 10, dirtWeight = 30, stoneWeight = 55, ironWeight = 15, goldWeight = 0  },
            new BlockLayer { maxDepth = 30, dirtWeight = 10, stoneWeight = 60, ironWeight = 25, goldWeight = 5  },
        };

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns a random depth within the allowed range.</summary>
        public int RollDepth() => UnityEngine.Random.Range(depthMin, depthMax + 1);

        /// <summary>Returns a random block type for the given depth.</summary>
        public BlockType RollBlockType(int depth)
        {
            // Finding a suitable layer
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
        [Tooltip("These weights are applied for depths 0..maxDepth")]
        public int maxDepth = 5;

        [Header("Weights (arbitrary sum, normalized automatically)")]
        public int dirtWeight  = 50;
        public int stoneWeight = 40;
        public int ironWeight  = 10;
        public int goldWeight  = 0;
    }
}
