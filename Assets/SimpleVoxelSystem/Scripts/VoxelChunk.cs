using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Рендерер отдельного участка (чанка) вокселей.
    /// Использует Job System + Burst для сборки геометрии без фризов.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxelChunk : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private Mesh mesh;

        // Координаты чанка в сетке Ворлда (умноженные на ChunkSize)
        public Vector3Int chunkPos;
        public int chunkSize = 16;
        
        // Буферы для Job
        private NativeList<Vector3> jobVerts;
        private NativeList<int> jobTris;
        private NativeList<Color> jobColors;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            mesh = new Mesh { name = "ChunkMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (jobVerts.IsCreated) jobVerts.Dispose();
            if (jobTris.IsCreated) jobTris.Dispose();
            if (jobColors.IsCreated) jobColors.Dispose();
        }

        public void Rebuild(NativeArray<byte> allVoxels, Vector3Int totalSize, NativeArray<Color> blockColors)
        {
            if (!jobVerts.IsCreated) jobVerts = new NativeList<Vector3>(1024, Allocator.Persistent);
            if (!jobTris.IsCreated) jobTris = new NativeList<int>(2048, Allocator.Persistent);
            if (!jobColors.IsCreated) jobColors = new NativeList<Color>(1024, Allocator.Persistent);

            jobVerts.Clear();
            jobTris.Clear();
            jobColors.Clear();

            var job = new VoxelMeshJob
            {
                voxels = allVoxels,
                dims = totalSize,
                chunkOffset = chunkPos,
                chunkSize = chunkSize,
                colors = blockColors,
                outVerts = jobVerts,
                outTris = jobTris,
                outColors = jobColors
            };

            JobHandle handle = job.Schedule();
            handle.Complete(); // Для WebGL лучше Complete сразу, либо через Coroutine в конце кадра

            UpdateMesh();
        }

        private void UpdateMesh()
        {
            mesh.Clear();
            if (jobVerts.Length == 0) return;

            mesh.SetVertices(jobVerts.AsArray());
            mesh.SetTriangles(jobTris.AsArray().ToArray(), 0);
            mesh.SetColors(jobColors.AsArray());

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }

        [BurstCompile]
        public struct VoxelMeshJob : IJob
        {
            [ReadOnly] public NativeArray<byte> voxels;
            [ReadOnly] public Vector3Int dims;
            [ReadOnly] public NativeArray<Color> colors;
            public Vector3Int chunkOffset;
            public int chunkSize;

            public NativeList<Vector3> outVerts;
            public NativeList<int> outTris;
            public NativeList<Color> outColors;

            public void Execute()
            {
                for (int x = 0; x < chunkSize; x++)
                for (int y = 0; y < chunkSize; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    int gx = x + chunkOffset.x;
                    int gy = y + chunkOffset.y;
                    int gz = z + chunkOffset.z;

                    if (gx >= dims.x || gy >= dims.y || gz >= dims.z) continue;

                    byte v = voxels[GetIdx(gx, gy, gz)];
                    if (v == 0) continue;

                    Color col = (v - 1 < colors.Length) ? colors[v - 1] : Color.magenta;
                    BuildBlock(gx, gy, gz, col);
                }
            }

            private void BuildBlock(int x, int y, int z, Color col)
            {
                Vector3 origin = new Vector3(x, -y, z);

                // Top
                if (IsAir(x, y - 1, z)) AddFace(origin, 0, col);
                // Bottom
                if (IsAir(x, y + 1, z)) AddFace(origin, 1, col);
                // Sides
                if (IsAir(x + 1, y, z)) AddFace(origin, 2, col);
                if (IsAir(x - 1, y, z)) AddFace(origin, 3, col);
                if (IsAir(x, y, z + 1)) AddFace(origin, 4, col);
                if (IsAir(x, y, z - 1)) AddFace(origin, 5, col);
            }

            private bool IsAir(int x, int y, int z)
            {
                if (x < 0 || y < 0 || z < 0 || x >= dims.x || y >= dims.y || z >= dims.z) return true;
                return voxels[GetIdx(x, y, z)] == 0;
            }

            private int GetIdx(int x, int y, int z) => x + (y * dims.x) + (z * dims.x * dims.y);

            private void AddFace(Vector3 origin, int faceIdx, Color col)
            {
                int baseIdx = outVerts.Length;

                // Используем те же смещения, что были в VoxelIsland
                switch (faceIdx)
                {
                    case 0: // TOP
                        outVerts.Add(origin + new Vector3(0, 1, 0));
                        outVerts.Add(origin + new Vector3(0, 1, 1));
                        outVerts.Add(origin + new Vector3(1, 1, 1));
                        outVerts.Add(origin + new Vector3(1, 1, 0));
                        break;
                    case 1: // BOTTOM
                        outVerts.Add(origin + new Vector3(0, 0, 1));
                        outVerts.Add(origin + new Vector3(0, 0, 0));
                        outVerts.Add(origin + new Vector3(1, 0, 0));
                        outVerts.Add(origin + new Vector3(1, 0, 1));
                        break;
                    case 2: // EAST (+X)
                        outVerts.Add(origin + new Vector3(1, 0, 0));
                        outVerts.Add(origin + new Vector3(1, 1, 0));
                        outVerts.Add(origin + new Vector3(1, 1, 1));
                        outVerts.Add(origin + new Vector3(1, 0, 1));
                        break;
                    case 3: // WEST (-X)
                        outVerts.Add(origin + new Vector3(0, 0, 1));
                        outVerts.Add(origin + new Vector3(0, 1, 1));
                        outVerts.Add(origin + new Vector3(0, 1, 0));
                        outVerts.Add(origin + new Vector3(0, 0, 0));
                        break;
                    case 4: // NORTH (+Z)
                        outVerts.Add(origin + new Vector3(1, 0, 1));
                        outVerts.Add(origin + new Vector3(1, 1, 1));
                        outVerts.Add(origin + new Vector3(0, 1, 1));
                        outVerts.Add(origin + new Vector3(0, 0, 1));
                        break;
                    case 5: // SOUTH (-Z)
                        outVerts.Add(origin + new Vector3(0, 0, 0));
                        outVerts.Add(origin + new Vector3(0, 1, 0));
                        outVerts.Add(origin + new Vector3(1, 1, 0));
                        outVerts.Add(origin + new Vector3(1, 0, 0));
                        break;
                }

                for (int i = 0; i < 4; i++) outColors.Add(col);

                outTris.Add(baseIdx + 0); outTris.Add(baseIdx + 1); outTris.Add(baseIdx + 2);
                outTris.Add(baseIdx + 0); outTris.Add(baseIdx + 2); outTris.Add(baseIdx + 3);
            }
        }
    }
}
