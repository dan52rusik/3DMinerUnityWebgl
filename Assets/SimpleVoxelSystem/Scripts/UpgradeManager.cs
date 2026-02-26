using UnityEngine;

namespace SimpleVoxelSystem
{
    public class UpgradeManager : MonoBehaviour
    {
        public PlayerPickaxe playerPickaxe;

        [Header("Upgrade Costs")]
        public int pickaxePowerCost = 100;
        public int backpackCapacityCost = 150;

        public void UpgradePickaxePower()
        {
            if (GlobalEconomy.Money >= pickaxePowerCost)
            {
                GlobalEconomy.Money -= pickaxePowerCost;
                playerPickaxe.pickaxePower++;
                
                // Увеличиваем стоимость следующего апгрейда
                pickaxePowerCost = Mathf.RoundToInt(pickaxePowerCost * 1.5f);
                
                Debug.Log($"Кирка улучшена! Сила: {playerPickaxe.pickaxePower}. Остаток денег: {GlobalEconomy.Money}");
            }
            else
            {
                Debug.Log($"Не хватает денег для улучшения кирки. Нужно {pickaxePowerCost}");
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
