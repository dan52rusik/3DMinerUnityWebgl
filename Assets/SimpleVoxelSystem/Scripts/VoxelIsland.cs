using UnityEngine;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;
using Unity.Collections;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Optimized version of VoxelIsland.
    /// Instead of one huge mesh, it splits the island into Chunks (16x16x16).
    /// Uses NativeArray to store data, allowing it to be passed to Job System.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxelIsland : MonoBehaviour
    {
        public int sizeX, sizeY, sizeZ;
        public int paddingX, paddingZ;
        public int chunkSize = 16;

        public int TotalX => sizeX + paddingX * 2;
        public int TotalY => sizeY;
        public int TotalZ => sizeZ + paddingZ * 2;

        // Voxel data in flat array (for Job System)
        private NativeArray<byte> voxels;
        private bool isDataCreated = false;

        /// <summary>
        /// Returns true only when voxel data is properly allocated and usable.
        /// After Domain Reload (script recompilation) NativeArray is invalidated
        /// even though the GameObject survives — this property detects that.
        /// </summary>
        public bool HasValidData => isDataCreated && voxels.IsCreated && voxels.Length > 0;

        public Color[] blockColors = new Color[]
        {
            new Color(0.55f, 0.27f, 0.07f),
            new Color(0.50f, 0.50f, 0.50f),
            new Color(0.65f, 0.44f, 0.40f),
            new Color(1.00f, 0.84f, 0.00f),
            new Color(0.20f, 0.40f, 0.90f),
        };
        private NativeArray<Color> blockColorsNative;

        private Dictionary<Vector3Int, VoxelChunk> chunks = new Dictionary<Vector3Int, VoxelChunk>();
        private GameObject chunksContainer;

        private void OnDestroy()
        {
            if (isDataCreated) voxels.Dispose();
            if (blockColorsNative.IsCreated) blockColorsNative.Dispose();
        }

        public void Init(int sx, int sy, int sz, int px, int pz)
        {
            sizeX = Mathf.Max(1, sx);
            sizeY = Mathf.Max(1, sy);
            sizeZ = Mathf.Max(1, sz);
            paddingX = px;
            paddingZ = pz;

            int total = TotalX * TotalY * TotalZ;
            if (isDataCreated) voxels.Dispose();
            
            // Use NativeArrayOptions.ClearMemory to ensure zero voxels
            voxels = new NativeArray<byte>(total, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            isDataCreated = true;

            SyncColors();
            CreateChunks();
        }

        public void SyncColors()
        {
            if (blockColorsNative.IsCreated) blockColorsNative.Dispose();
            
            // Dynamically determine color array size based on enum.
            // Grass = 5, so we need at least 6 slots (indices 0-5).
            int maxEnumVal = 0;
            foreach (int val in System.Enum.GetValues(typeof(BlockType))) 
                if (val > maxEnumVal) maxEnumVal = val;

            Color[] finalCols = new Color[maxEnumVal + 1];
            for (int i = 0; i < finalCols.Length; i++)
            {
                if (i < blockColors.Length) finalCols[i] = blockColors[i];
                else finalCols[i] = Color.gray; // Fallback
            }

            // Direct support for Grass (5) if array is shorter
            if (maxEnumVal >= 5)
            {
                 // If Grass is not yet set or beyond blockColors
                 if (finalCols[5] == Color.gray || finalCols[5] == Color.clear)
                     finalCols[5] = new Color(0.2f, 0.8f, 0.2f); // Green for Grass
            }

            blockColorsNative = new NativeArray<Color>(finalCols, Allocator.Persistent);
        }

        private void CreateChunks()
        {
            if (chunksContainer != null) Destroy(chunksContainer);
            chunksContainer = new GameObject("Chunks");
            chunksContainer.transform.SetParent(this.transform, false);
            chunks.Clear();

            MeshRenderer parentMR = GetComponent<MeshRenderer>();
            Material sharedMat = (parentMR != null) ? parentMR.sharedMaterial : null;

            for (int x = 0; x < TotalX; x += chunkSize)
            for (int y = 0; y < TotalY; y += chunkSize)
            for (int z = 0; z < TotalZ; z += chunkSize)
            {
                Vector3Int pos = new Vector3Int(x, y, z);
                GameObject chunkGO = new GameObject($"Chunk_{x}_{y}_{z}");
                chunkGO.transform.SetParent(chunksContainer.transform, false);
                chunkGO.transform.localPosition = Vector3.zero;

                VoxelChunk chunk = chunkGO.AddComponent<VoxelChunk>();
                chunk.chunkPos = pos;
                chunk.chunkSize = chunkSize;

                if (sharedMat != null)
                {
                    chunk.GetComponent<MeshRenderer>().sharedMaterial = sharedMat;
                }

                chunks.Add(pos, chunk);
            }
            
            if (parentMR != null) parentMR.enabled = false;
        }

        public void SetVoxel(int x, int y, int z, BlockType type, bool rebuildMesh = false)
        {
            if (!InBounds(x, y, z)) return;
            voxels[GetIdx(x, y, z)] = (byte)((int)type + 1);
            if (rebuildMesh)
                RebuildAffectedChunks(x, y, z);
        }

        public bool TryGetBlockType(int x, int y, int z, out BlockType type)
        {
            if (!InBounds(x, y, z))
            {
                type = BlockType.Dirt;
                return false;
            }
            byte v = voxels[GetIdx(x, y, z)];
            if (v == 0)
            {
                type = BlockType.Dirt;
                return false;
            }
            type = (BlockType)(v - 1);
            return true;
        }

        public void RemoveVoxel(int x, int y, int z, bool rebuildMesh = true)
        {
            if (!InBounds(x, y, z)) return;
            voxels[GetIdx(x, y, z)] = 0;
            if (rebuildMesh)
                RebuildAffectedChunks(x, y, z);
        }

        public bool IsSolid(int x, int y, int z)
        {
            if (!InBounds(x, y, z)) return false;
            return voxels[GetIdx(x, y, z)] != 0;
        }

        public void RebuildMesh()
        {
            SyncColors();
            Vector3Int dims = new Vector3Int(TotalX, TotalY, TotalZ);
            foreach (var chunk in chunks.Values)
            {
                chunk.Rebuild(voxels, dims, blockColorsNative);
            }
            Physics.SyncTransforms();
        }

        private void RebuildAffectedChunks(int x, int y, int z)
        {
            Vector3Int dims = new Vector3Int(TotalX, TotalY, TotalZ);
            
            // Main chunk
            RebuildChunkAt(x, y, z, dims);

            // If block is on chunk boundary, we need to update the neighbor as well (since a face might open up)
            if (x % chunkSize == 0) RebuildChunkAt(x - 1, y, z, dims);
            if ((x + 1) % chunkSize == 0) RebuildChunkAt(x + 1, y, z, dims);
            if (y % chunkSize == 0) RebuildChunkAt(x, y - 1, z, dims);
            if ((y + 1) % chunkSize == 0) RebuildChunkAt(x, y + 1, z, dims);
            if (z % chunkSize == 0) RebuildChunkAt(x, y, z - 1, dims);
            if ((z + 1) % chunkSize == 0) RebuildChunkAt(x, y, z + 1, dims);
        }

        private void RebuildChunkAt(int x, int y, int z, Vector3Int dims)
        {
            if (!InBounds(x, y, z)) return;
            int cx = (x / chunkSize) * chunkSize;
            int cy = (y / chunkSize) * chunkSize;
            int cz = (z / chunkSize) * chunkSize;
            Vector3Int cpos = new Vector3Int(cx, cy, cz);

            if (chunks.TryGetValue(cpos, out VoxelChunk chunk))
                chunk.Rebuild(voxels, dims, blockColorsNative);
        }

        private int GetIdx(int x, int y, int z) => x + (y * TotalX) + (z * TotalX * TotalY);

        public bool InBounds(int x, int y, int z)
            => x >= 0 && x < TotalX
            && y >= 0 && y < TotalY
            && z >= 0 && z < TotalZ;

        public bool IsInBounds(int x, int y, int z) => InBounds(x, y, z);

        public Vector3 GridToLocal(int x, int y, int z) => new Vector3(x, -y, z);

        public Vector3Int LocalToGrid(Vector3 local)
        {
            return new Vector3Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(-local.y), Mathf.RoundToInt(local.z));
        }

        public void ClearVoxels()
        {
            if (isDataCreated)
            {
                for (int i = 0; i < voxels.Length; i++) voxels[i] = 0;
            }
        }
    }
}
