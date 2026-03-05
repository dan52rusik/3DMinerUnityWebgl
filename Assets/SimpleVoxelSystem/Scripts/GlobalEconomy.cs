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
            // Now the server does this. The client just waits for updates via NetworkVariable.
            // But for instant UI feedback, we can leave the local logic (prediction)
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
