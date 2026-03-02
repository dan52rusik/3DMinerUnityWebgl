using UnityEngine;

namespace SimpleVoxelSystem
{
    public static class GlobalEconomy
    {
        public static int Money = 300;
        public static int MiningXP = 0;
        public static int MiningLevel = 1;

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
            int nextLevelThreshold = MiningLevel * 50;
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
