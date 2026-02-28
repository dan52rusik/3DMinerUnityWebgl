using UnityEngine;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Хранит весь массив воксельных данных острова и строит единый меш.
    /// Весь остров = 1 MeshRenderer = 1 draw call (vertex colors).
    ///
    /// Система координат сетки:
    ///   y = 0   — поверхность (верхний слой)
    ///   y = N   — N единиц НИЖЕ поверхности
    /// В локальных координатах Unity: блок с grid.y = Y расположен на localY = -Y
    /// (т.е. глубина идёт в отрицательный Y).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class VoxelIsland : MonoBehaviour
    {
        // ─── Размеры ───────────────────────────────────────────────────────────
        public int sizeX;        // ширина колодца
        public int sizeY;        // глубина колодца
        public int sizeZ;        // длина колодца
        public int paddingX;     // паддинг по X (земля вокруг)
        public int paddingZ;     // паддинг по Z

        public int TotalX => sizeX + paddingX * 2;
        public int TotalY => sizeY;
        public int TotalZ => sizeZ + paddingZ * 2;

        // ─── Данные ────────────────────────────────────────────────────────────
        // 0 = воздух, 1+ = (int)BlockType + 1
        private byte[,,] voxels;

        // ─── Цвета ────────────────────────────────────────────────────────────
        // Синхронизируются из BlockDataConfig через WellGenerator
        public Color[] blockColors = new Color[]
        {
            new Color(0.55f, 0.27f, 0.07f), // Dirt  — коричневый
            new Color(0.50f, 0.50f, 0.50f), // Stone — серый
            new Color(0.65f, 0.44f, 0.40f), // Iron  — ржавый
            new Color(1.00f, 0.84f, 0.00f), // Gold  — жёлтый
            new Color(0.20f, 0.40f, 0.90f), // Shop  — синий (для магазина)
        };

        // ─── Компоненты ────────────────────────────────────────────────────────
        private MeshFilter   meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Mesh         mesh;

        // ─── Буферы для построения меша ────────────────────────────────────────
        private readonly List<Vector3> verts  = new List<Vector3>(8192);
        private readonly List<int>     tris   = new List<int>(16384);
        private readonly List<Color>   colors = new List<Color>(8192);

        // ══════════════════════════════════════════════════════════════════════
        // Публичное API
        // ══════════════════════════════════════════════════════════════════════

        public void Init(int sx, int sy, int sz, int px, int pz)
        {
            sizeX = Mathf.Max(1, sx);
            sizeY = Mathf.Max(1, sy);
            sizeZ = Mathf.Max(1, sz);
            paddingX = px;
            paddingZ = pz;
            voxels = new byte[TotalX, TotalY, TotalZ];
        }

        public void SetVoxel(int x, int y, int z, BlockType type)
        {
            if (!InBounds(x, y, z)) return;
            voxels[x, y, z] = (byte)((int)type + 1);
        }

        public bool TryGetBlockType(int x, int y, int z, out BlockType type)
        {
            if (!InBounds(x, y, z) || voxels[x, y, z] == 0)
            {
                type = BlockType.Dirt;
                return false;
            }
            type = (BlockType)(voxels[x, y, z] - 1);
            return true;
        }

        public void RemoveVoxel(int x, int y, int z)
        {
            RemoveVoxel(x, y, z, true);
        }

        public void RemoveVoxel(int x, int y, int z, bool rebuildMesh)
        {
            if (!InBounds(x, y, z)) return;
            voxels[x, y, z] = 0;
            if (rebuildMesh)
                RebuildMesh();
        }

        public bool IsSolid(int x, int y, int z)
        {
            if (!InBounds(x, y, z)) return false;
            return voxels[x, y, z] != 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Построение меша
        // ══════════════════════════════════════════════════════════════════════

        public void RebuildMesh()
        {
            EnsureComponents();
            verts.Clear();
            tris.Clear();
            colors.Clear();

            for (int x = 0; x < TotalX; x++)
            for (int y = 0; y < TotalY; y++)
            for (int z = 0; z < TotalZ; z++)
            {
                if (voxels[x, y, z] == 0) continue;
                BuildBlockFaces(x, y, z);
            }

            if (mesh == null)
                mesh = new Mesh { name = "VoxelIslandMesh" };
            else
                mesh.Clear();

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(colors);

            // RecalculateNormals корректно работает, т.к. каждая грань имеет
            // собственные 4 вершины (не разделяемые с соседями)
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            meshFilter.sharedMesh   = mesh;
            meshCollider.sharedMesh = mesh;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Грани — ядро системы
        // ══════════════════════════════════════════════════════════════════════

        // Сетка: y=0 = верх, y растёт вниз.
        // World localY = -gridY, поэтому origin блока = (x, -y, z).
        // Блок занимает локальное пространство: [x, x+1] × [-y, -y+1] × [z, z+1].
        //
        // Проверки видимости граней:
        //   Верхняя  (+Y нормаль): является ли блок выше (grid y-1) воздухом?
        //   Нижняя   (-Y нормаль): является ли блок ниже  (grid y+1) воздухом?
        //
        // Порядок вершин: CCW (contre le sens des aiguilles d'une montre) при виде
        // снаружи = стандарт Unity / WebGL (OpenGL ES CCW = лицевая сторона).

        // Заранее заданные вершины граней. Ключ: относительно origin = (x,-y,z).
        // top-уровень блока = localY+1, bottom = localY+0.

        //  Y
        //  |  Z
        //  | /
        //  |/___X
        //
        // Каждая грань — 4 вершины, образующие quad:
        // tri1: 0,1,2  tri2: 0,2,3

        private static readonly Vector3[][] FaceVerts = new[]
        {
            // 0 — TOP (+Y нормаль), y_offset = 1
            // Проверено: v01=(0,0,1) × v02=(1,0,1) → normal=(0,+1,0) ✓
            new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },

            // 1 — BOTTOM (-Y нормаль), y_offset = 0
            // Проверено: v01=(0,0,-1) × v02=(1,0,-1) → normal=(0,-1,0) ✓
            new[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },

            // 2 — EAST (+X нормаль), x_offset = 1
            // Вид со стороны +X: CCW в YZ
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },

            // 3 — WEST (-X нормаль), x_offset = 0
            // Вид со стороны -X: CCW в YZ
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },

            // 4 — NORTH (+Z нормаль), z_offset = 1
            // Вид со стороны +Z: CCW в XY
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },

            // 5 — SOUTH (-Z нормаль), z_offset = 0
            // Вид со стороны -Z: CCW в XY
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
        };

        private void BuildBlockFaces(int x, int y, int z)
        {
            Color col = GetColor(voxels[x, y, z]);
            // Локальный origin блока. Grid-y инвертирован в world Y.
            Vector3 origin = new Vector3(x, -y, z);

            // TOP: выше в сетке = меньший y-индекс
            if (!IsSolid(x, y - 1, z)) AddFace(origin, 0, col);
            // BOTTOM: ниже в сетке = больший y-индекс
            if (!IsSolid(x, y + 1, z)) AddFace(origin, 1, col);
            // X соседи
            if (!IsSolid(x + 1, y, z)) AddFace(origin, 2, col);
            if (!IsSolid(x - 1, y, z)) AddFace(origin, 3, col);
            // Z соседи
            if (!IsSolid(x, y, z + 1)) AddFace(origin, 4, col);
            if (!IsSolid(x, y, z - 1)) AddFace(origin, 5, col);
        }

        private void AddFace(Vector3 origin, int faceIdx, Color col)
        {
            int baseIdx = verts.Count;
            Vector3[] fv = FaceVerts[faceIdx];

            for (int i = 0; i < 4; i++)
            {
                verts.Add(origin + fv[i]);
                colors.Add(col);
            }

            tris.Add(baseIdx + 0);
            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 2);

            tris.Add(baseIdx + 0);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 3);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Вспомогательные
        // ══════════════════════════════════════════════════════════════════════

        private void EnsureComponents()
        {
            if (meshFilter   == null) meshFilter   = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
        }

        private bool InBounds(int x, int y, int z)
            => x >= 0 && x < TotalX
            && y >= 0 && y < TotalY
            && z >= 0 && z < TotalZ;

        // voxelByte — сырое значение из массива (1+ = блок)
        private Color GetColor(byte voxelByte)
        {
            int idx = voxelByte - 1;
            if (idx < 0 || idx >= blockColors.Length) return Color.magenta;
            return blockColors[idx];
        }

        // Конвертировать сеточную позицию в локальное пространство (для UI/дебага)
        public Vector3 GridToLocal(int x, int y, int z) => new Vector3(x, -y, z);
    }
}
