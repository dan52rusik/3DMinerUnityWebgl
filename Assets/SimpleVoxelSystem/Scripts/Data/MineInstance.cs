using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Data about an active (purchased, but not yet exhausted) mine.
    /// Created when purchased, destroyed when sold/exhausted.
    /// </summary>
    public class MineInstance
    {
        public MineShopData shopData;       // ScriptableObject with settings
        public int          rolledDepth;    // Real depth (rolled at purchase)
        public int          totalBlocks;    // Number of blocks during generation
        public int          minedBlocks;    // Number of blocks already mined

        // Stores block types so they don't change on reloads
        private byte[,,]    voxelsData; 

        public System.Collections.Generic.HashSet<Vector3Int> minedPositions = new System.Collections.Generic.HashSet<Vector3Int>();

        public int originX, originZ; // placement coordinates on the island
        public bool IsExhausted => totalBlocks > 0 && minedBlocks >= totalBlocks;

        /// <summary>Sale price of an exhausted mine.</summary>
        public int SellPrice => Mathf.RoundToInt(shopData.buyPrice * shopData.sellBackRatio);

        public MineInstance(MineShopData data, int depth, int totalBlockCount)
        {
            shopData    = data;
            rolledDepth = depth;
            totalBlocks = totalBlockCount;
            minedBlocks = 0;
        }

        public void InitializeVoxels(int ww, int wl, int wd)
        {
            if (voxelsData != null) return;
            voxelsData = new byte[ww, wd, wl];
        }

        public void SetVoxel(int lx, int ly, int lz, BlockType type)
        {
            if (voxelsData == null || !InBounds(lx, ly, lz)) return;
            voxelsData[lx, ly, lz] = (byte)((int)type + 1);
        }

        public BlockType GetVoxel(int lx, int ly, int lz)
        {
            if (voxelsData == null || !InBounds(lx, ly, lz)) return BlockType.Air;
            int b = voxelsData[lx, ly, lz];
            return (b == 0) ? BlockType.Air : (BlockType)(b - 1);
        }

        public bool HasVoxelsData => voxelsData != null;

        public bool HasVoxelValue(int lx, int ly, int lz)
            => voxelsData != null && InBounds(lx, ly, lz) && voxelsData[lx, ly, lz] != 0;

        public void RegisterMinedBlock(int x, int y, int z)
        {
            // Save local coordinates (relative to the start of the mine)
            if (minedPositions.Add(new Vector3Int(x - originX, y, z - originZ)))
                minedBlocks++;
        }

        public bool IsVoxelMined(int x, int y, int z) => minedPositions.Contains(new Vector3Int(x - originX, y, z - originZ));

        private bool InBounds(int lx, int ly, int lz)
            => lx >= 0 && ly >= 0 && lz >= 0
            && lx < voxelsData.GetLength(0)
            && ly < voxelsData.GetLength(1)
            && lz < voxelsData.GetLength(2);
    }
}  
