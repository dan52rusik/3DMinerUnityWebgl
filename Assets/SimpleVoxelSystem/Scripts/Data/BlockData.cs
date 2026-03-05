using System;
using UnityEngine;

namespace SimpleVoxelSystem.Data
{
    public enum BlockType { Air = 0, Dirt = 1, Stone = 2, Iron = 3, Gold = 4, Grass = 5 }

    [Serializable]
    public class BlockData 
    {
        public BlockType type;
        public int maxHealth; // Hits required
        public int reward;    // Coins/resources reward
        public int xpReward;  // XP reward for mining
        public int requiredMiningLevel; // Required level for mining
        public Color blockColor; // Color or material reference
    }
}
