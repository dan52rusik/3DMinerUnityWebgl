using UnityEngine;

namespace SimpleVoxelSystem
{
    public static class GlobalEconomy
    {
        public static int Money = 300;
        public static int MiningXP = 0;
        public static int MiningLevel = 1;

        public static bool AddMiningXP(int amount)
        {
            MiningXP += amount;
            int nextLevelThreshold = MiningLevel * 50; // Быстрее в начале
            if (MiningXP >= nextLevelThreshold)
            {
                MiningXP -= nextLevelThreshold;
                MiningLevel++;
                Debug.Log($"<color=cyan>✨ УРОВЕНЬ ПОВЫШЕН! Текущий уровень копки: {MiningLevel}</color>");
                return true;
            }
            return false;
        }
    }
}
