using UnityEngine;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class WellGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int width = 5;
        public int length = 5;
        public int depth = 10;
        public int padding = 5; // Размер участка земли вокруг
        public float blockSize = 1f;

        [Header("Config")]
        // Настройте в инспекторе данные для Dirt, Stone, Iron, Gold
        public List<BlockData> blockDataConfig; 

        // Логика слоев
        private int topLayerDepth = 3;
        private int midLayerDepth = 7;

        // Пул (трехмерный массив)
        private Block[,,] blockGrid;

        void Start()
        {
            if (blockDataConfig == null || blockDataConfig.Count == 0)
            {
                Debug.LogWarning("Создайте настройки для блоков (BlockDataConfig) в инспекторе Generator!");
                return;
            }
            GenerateWell();
        }

        void GenerateWell()
        {
            blockGrid = new Block[width, depth, length];
            Vector3 startPos = transform.position;

            // Генерация окружающего террейна (земли)
            for (int x = -padding; x < width + padding; x++)
            {
                for (int z = -padding; z < length + padding; z++)
                {
                    // Пропускаем зону самого колодца, так как там свои блоки
                    if (x >= 0 && x < width && z >= 0 && z < length) continue;

                    Vector3 pos = startPos + new Vector3(x * blockSize, 0, z * blockSize);
                    
                    GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.position = pos;
                    obj.transform.SetParent(transform);
                    
                    Block block = obj.AddComponent<Block>();
                    BlockData data = blockDataConfig.Find(d => d.type == BlockType.Dirt);
                    
                    if (data != null)
                    {
                        block.Initialize(data, x, 0, z);
                    }
                }
            }

            for (int y = 0; y < depth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < length; z++)
                    {
                        Vector3 pos = startPos + new Vector3(x * blockSize, -y * blockSize, z * blockSize);
                        
                        // Создаем куб средствами Unity
                        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        obj.transform.position = pos;
                        obj.transform.SetParent(transform);
                        
                        // Добавляем наш компонент Block
                        Block block = obj.AddComponent<Block>();
                        
                        BlockType typeToSpawn = DetermineBlockTypeForDepth(y);
                        BlockData data = blockDataConfig.Find(d => d.type == typeToSpawn);
                        
                        if (data != null)
                        {
                            block.Initialize(data, x, y, z);
                        }

                        blockGrid[x, y, z] = block;
                    }
                }
            }
        }

        // Вызовем из кирки
        public void MineBlockAndPool(Block block)
        {
            if (block != null && block.gameObject.activeSelf)
            {
                block.gameObject.SetActive(false);
                
                int x = block.gridX;
                int y = block.gridY;
                int z = block.gridZ;

                // Если сломанный блок находился внутри колодца, применяем логику сдвига
                if (x >= 0 && x < width && y >= 0 && y < depth && z >= 0 && z < length)
                {
                    // В будущем: сдвигать колонну вниз
                    // Или переносить отключенный блок под слой с новым типом (пулинг)
                }
            }
        }

        private BlockType DetermineBlockTypeForDepth(int depthIndex)
        {
            // Самый верхний слой (индекс 0) - всегда только Земля
            if (depthIndex == 0)
            {
                return BlockType.Dirt;
            }

            float rand = Random.value;

            if (depthIndex < topLayerDepth)
            {
                return rand < 0.9f ? BlockType.Dirt : BlockType.Stone;
            }
            else if (depthIndex < midLayerDepth)
            {
                if (rand < 0.5f) return BlockType.Stone;
                if (rand < 0.8f) return BlockType.Dirt;
                return BlockType.Iron;
            }
            else
            {
                return rand < 0.8f ? BlockType.Stone : BlockType.Gold;
            }
        }
    }
}
