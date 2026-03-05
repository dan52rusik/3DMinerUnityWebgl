using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class Block : MonoBehaviour
    {
        public BlockType type;
        public int currentHealth;
        public BlockData currentData;

        // Grid coordinates
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
                // For simplicity and performance, we change the material color.
                // It's better to use MaterialPropertyBlock for WebGL in a larger project.
                meshRenderer.material.color = data.blockColor;
            }
        }
    }
}
