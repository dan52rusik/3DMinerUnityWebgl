using System;
using UnityEngine;

namespace SimpleVoxelSystem
{
    public static class GlobalEconomy
    {
        // FIX #15: свойства с событиями вместо голых полей — реактивный подход без поллинга

        public static event Action<int> OnMoneyChanged;
        public static event Action<int> OnXPChanged;
        public static event Action<int> OnLevelChanged;

        private static int _money    = EconomyTuning.StartMoney;
        private static int _miningXP = EconomyTuning.StartMiningXP;
        private static int _miningLevel = EconomyTuning.StartMiningLevel;

        public static int Money
        {
            get => _money;
            set { if (_money == value) return; _money = value; OnMoneyChanged?.Invoke(_money); }
        }

        public static int MiningXP
        {
            get => _miningXP;
            set { if (_miningXP == value) return; _miningXP = value; OnXPChanged?.Invoke(_miningXP); }
        }

        public static int MiningLevel
        {
            get => _miningLevel;
            set { if (_miningLevel == value) return; _miningLevel = value; OnLevelChanged?.Invoke(_miningLevel); }
        }

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
