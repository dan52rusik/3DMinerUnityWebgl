using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class Block : MonoBehaviour
    {
        public BlockType type;
        public int currentHealth;
        public BlockData currentData;

        // Координаты в сетке генератора
        public int gridX, gridY, gridZ;

        private MeshRenderer meshRenderer;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        public void Initialize(BlockData data, int x, int y, int z)
        {
            currentData = data;
            type = data.type;
            currentHealth = data.maxHealth;
            gridX = x; gridY = y; gridZ = z;

            if (meshRenderer != null)
            {
                // Для простоты и производительности меняем цвет материала
                // Лучше использовать MaterialPropertyBlock для webgl
                meshRenderer.material.color = data.blockColor;
            }
        }
    }
}
