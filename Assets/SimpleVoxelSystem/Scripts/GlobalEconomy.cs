using UnityEngine;

namespace SimpleVoxelSystem
{
    public static class GlobalEconomy
    {
        public static int Money = EconomyTuning.StartMoney;
        public static int MiningXP = EconomyTuning.StartMiningXP;
        public static int MiningLevel = EconomyTuning.StartMiningLevel;

        public static void SyncFromNetwork(int money, int xp, int level)
        {
            Money = money;
            MiningXP = xp;
            MiningLevel = level;
        }

        public static bool AddMiningXP(int amount)
        {
            // Теперь это делает сервер. Клиент просто ждет обновления через NetworkVariable.
            // Но для мгновенного UI фидбека можно оставить локальную логику (предикция)
            MiningXP += amount;
            int nextLevelThreshold = MiningLevel * EconomyTuning.MiningXpPerLevelMultiplier;
            if (MiningXP >= nextLevelThreshold)
            {
                MiningXP -= nextLevelThreshold;
                MiningLevel++;
                return true;
            }
            return false;
        }
    }
}
