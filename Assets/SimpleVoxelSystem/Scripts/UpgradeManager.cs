using UnityEngine;

namespace SimpleVoxelSystem
{
    public class UpgradeManager : MonoBehaviour
    {
        public PlayerPickaxe playerPickaxe;

        [Header("Upgrade Costs")]
        public int playerStrengthCost = 100;
        public int backpackCapacityCost = 150;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<UpgradeManager>() != null)
                return;

            GameObject go = new GameObject("UpgradeManager");
            DontDestroyOnLoad(go);
            go.AddComponent<UpgradeManager>();
        }

        private void Awake()
        {
            if (playerPickaxe == null)
                playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();

            if (GetComponent<UpgradeHUD>() == null)
                gameObject.AddComponent<UpgradeHUD>();
        }

        public void UpgradePlayerStrength()
        {
            if (GlobalEconomy.Money >= playerStrengthCost)
            {
                GlobalEconomy.Money -= playerStrengthCost;
                playerPickaxe.playerStrength++;
                
                // Увеличиваем стоимость следующего апгрейда
                playerStrengthCost = Mathf.RoundToInt(playerStrengthCost * 1.5f);
                
                Debug.Log($"Сила персонажа улучшена! Бонус: +{playerPickaxe.playerStrength}. Остаток денег: {GlobalEconomy.Money}");
            }
            else
            {
                Debug.Log($"Не хватает денег для улучшения силы. Нужно {playerStrengthCost}");
            }
        }

        public void UpgradeBackpackCapacity()
        {
            if (GlobalEconomy.Money >= backpackCapacityCost)
            {
                GlobalEconomy.Money -= backpackCapacityCost;
                playerPickaxe.maxBackpackCapacity += 5; // Увеличиваем на 5 мест
                
                // Увеличиваем стоимость
                backpackCapacityCost = Mathf.RoundToInt(backpackCapacityCost * 1.5f);
                
                Debug.Log($"Рюкзак улучшен! Вместимость: {playerPickaxe.maxBackpackCapacity}. Остаток денег: {GlobalEconomy.Money}");
            }
            else
            {
                Debug.Log($"Не хватает денег для улучшения рюкзака. Нужно {backpackCapacityCost}");
            }
        }
    }
}
