using UnityEngine;

namespace SimpleVoxelSystem
{
    public class PlayerPickaxe : MonoBehaviour
    {
        [Header("Mining Setup")]
        public int pickaxePower = 1;
        public float miningRange = 5f;
        public WellGenerator wellGenerator; // Ссылка на генератор для отключения блоков в пуле

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                MineRaycast();
            }
        }

        void MineRaycast()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, miningRange))
            {
                Block block = hit.collider.GetComponent<Block>();
                if (block != null)
                {
                    MineBlock(block);
                }
            }
        }

        void MineBlock(Block block)
        {
            block.currentHealth -= pickaxePower;
            
            // Здесь можно добавить:
            // SpawnParticles(block.transform.position);
            // CameraShake();

            if (block.currentHealth <= 0)
            {
                CollectResources(block.currentData);

                // Если есть ссылка на генератор, отключаем через него для Object Pooling
                if (wellGenerator != null)
                {
                    wellGenerator.MineBlockAt(block.gridX, block.gridY, block.gridZ);
                }
                else
                {
                    block.gameObject.SetActive(false);
                }
            }
        }

        void CollectResources(Data.BlockData data)
        {
            // Здесь в будущем будет добавлено в систему инвентаря
            Debug.Log($"Добыт ресурс: {data.type}, Награда: {data.reward}");
        }
    }
}
