using UnityEngine;

namespace SimpleVoxelSystem
{
    public class UpgradeManager : MonoBehaviour
    {
        public PlayerPickaxe playerPickaxe;

        [Header("Upgrade Costs")]
        public int playerStrengthCost = EconomyTuning.PlayerStrengthUpgradeStartCost;
        public int backpackCapacityCost = EconomyTuning.BackpackUpgradeStartCost;

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

            if (playerStrengthCost <= 0)
                playerStrengthCost = EconomyTuning.PlayerStrengthUpgradeStartCost;
            if (backpackCapacityCost <= 0)
                backpackCapacityCost = EconomyTuning.BackpackUpgradeStartCost;

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
                playerStrengthCost = Mathf.RoundToInt(playerStrengthCost * EconomyTuning.PlayerUpgradeCostMultiplier);
                
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
                playerPickaxe.maxBackpackCapacity += EconomyTuning.BackpackCapacityPerUpgrade;
                
                // Увеличиваем стоимость
                backpackCapacityCost = Mathf.RoundToInt(backpackCapacityCost * EconomyTuning.PlayerUpgradeCostMultiplier);
                
                Debug.Log($"Рюкзак улучшен! Вместимость: {playerPickaxe.maxBackpackCapacity}. Остаток денег: {GlobalEconomy.Money}");
            }
            else
            {
                Debug.Log($"Не хватает денег для улучшения рюкзака. Нужно {backpackCapacityCost}");
            }
        }
    }
}
