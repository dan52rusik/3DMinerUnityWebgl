using UnityEngine;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Генератор острова. Заполняет VoxelIsland данными — никаких отдельных GameObject-ов.
    /// Весь остров рисуется за 1 draw call.
    /// </summary>
    [RequireComponent(typeof(VoxelIsland))]
    public class WellGenerator : MonoBehaviour
    {
        [Header("Размер Колодца")]
        public int wellWidth  = 5;
        public int wellLength = 5;
        public int wellDepth  = 10;

        [Header("Паддинг — земля вокруг колодца")]
        public int padding = 5;

        [Header("Блоки")]
        public List<BlockData> blockDataConfig;

        // Слои
        private const int TopLayerDepth = 3;
        private const int MidLayerDepth = 7;

        private VoxelIsland island;

        // ──────────────────────────────────────────────────────────────────────
        void Start()
        {
            if (blockDataConfig == null || blockDataConfig.Count == 0)
            {
                Debug.LogWarning("[WellGenerator] Нет blockDataConfig в инспекторе!");
                return;
            }

            island = GetComponent<VoxelIsland>();

            // Передаём цвета блоков в VoxelIsland
            SyncColorsToIsland();

            // Инициализируем сетку (только один раз)
            island.Init(
                wellWidth,
                wellDepth,
                wellLength,
                padding,
                padding
            );

            // Генерируем стартовый остров
            GenerateStartIsland();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Генерация

        void GenerateStartIsland()
        {
            // 1) Поверхность земли вокруг колодца (y=0, весь TotalX × TotalZ)
            for (int x = 0; x < island.TotalX; x++)
            for (int z = 0; z < island.TotalZ; z++)
            {
                // В зоне колодца поверхность не ставим — там провал
                bool inWell = (x >= padding && x < padding + wellWidth) &&
                              (z >= padding && z < padding + wellLength);
                if (!inWell)
                    island.SetVoxel(x, 0, z, BlockType.Dirt);
            }

            // 2) Колонны блоков внутри колодца (x/z идут со смещением padding)
            for (int lx = 0; lx < wellWidth;  lx++)
            for (int lz = 0; lz < wellLength; lz++)
            for (int y  = 0; y  < wellDepth;  y++)
            {
                int wx = lx + padding;
                int wz = lz + padding;
                BlockType t = DetermineBlockType(y);
                island.SetVoxel(wx, y, wz, t);
            }

            island.RebuildMesh();

            Debug.Log($"[WellGenerator] Остров построен. Статистика: " +
                      $"{island.TotalX}x{island.TotalY}x{island.TotalZ} вокселей → 1 draw call.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Публичное API

        /// <summary>
        /// Добыча блока: удалить вокель и перестроить меш.
        /// Вызывается из PlayerPickaxe.
        /// </summary>
        public void MineVoxel(int gx, int gy, int gz)
        {
            island.RemoveVoxel(gx, gy, gz);
        }

        /// <summary>
        /// Расширение острова (через LandPlot). Добавляет новый ряд/участок.
        /// offsetX/offsetZ — смещение в воксельных координатах.
        /// </summary>
        public void GeneratePlotExtension(int offsetX, int offsetZ, int width, int length)
        {
            // TODO: при реализации «Пути А» — расширяем массив через island.Resize()
            // и добавляем новые воксели здесь.
            Debug.Log($"[WellGenerator] Покупка участка +[{offsetX},{offsetZ}] size {width}x{length}");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Вспомогательные

        private BlockType DetermineBlockType(int depthIndex)
        {
            if (depthIndex == 0)             return BlockType.Dirt;

            float rand = Random.value;
            if (depthIndex < TopLayerDepth)
                return rand < 0.9f ? BlockType.Dirt : BlockType.Stone;

            if (depthIndex < MidLayerDepth)
            {
                if (rand < 0.5f) return BlockType.Stone;
                if (rand < 0.8f) return BlockType.Dirt;
                return BlockType.Iron;
            }

            return rand < 0.8f ? BlockType.Stone : BlockType.Gold;
        }

        private void SyncColorsToIsland()
        {
            int typeCount = System.Enum.GetValues(typeof(BlockType)).Length;

            // Начинаем с дефолтных цветов из VoxelIsland (они всегда корректны)
            Color[] cols = new Color[typeCount];
            for (int i = 0; i < typeCount && i < island.blockColors.Length; i++)
                cols[i] = island.blockColors[i];

            // Перезаписываем только те блоки, у которых задан ненулевой цвет в инспекторе
            foreach (var bd in blockDataConfig)
            {
                int idx = (int)bd.type;
                if (idx < typeCount && bd.blockColor.a > 0.01f)
                    cols[idx] = bd.blockColor;
            }

            island.blockColors = cols;

            // Дебаг: выводим цвета в консоль для проверки
            for (int i = 0; i < cols.Length; i++)
                Debug.Log($"[WellGenerator] BlockColor[{(BlockType)i}] = {cols[i]}");
        }
    }
}
