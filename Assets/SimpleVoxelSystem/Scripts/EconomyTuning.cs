namespace SimpleVoxelSystem
{
    /// <summary>
    /// Single source of truth for economy tuning values:
    /// prices, upgrade costs, progression thresholds and default shop offers.
    /// </summary>
    public static class EconomyTuning
    {
        // Core progression
        public const int StartMoney = 300;
        public const int StartMiningXP = 0;
        public const int StartMiningLevel = 1;
        public const int MiningXpPerLevelMultiplier = 50;

        // Player upgrades
        public const int PlayerStrengthUpgradeStartCost = 100;
        public const int BackpackUpgradeStartCost = 150;
        public const float PlayerUpgradeCostMultiplier = 1.5f;
        public const int BackpackCapacityPerUpgrade = 5;

        // Minion economy
        public const int MinionPurchasePrice = 1000;
        public const int MinionStrengthUpgradeCost = 200;
        public const int MinionCapacityUpgradeCost = 200;
        public const int MinionCapacityPerUpgrade = 5;

        // Land/plot economy
        public const int LandPlotPurchasePrice = 500;

        // Default mine setup
        public const int DefaultMineWellWidth = 5;
        public const int DefaultMineWellLength = 5;
        public const int DefaultMinePadding = 3;

        public const int BronzeMinePrice = 300;
        public const float BronzeMineSellBackRatio = 0.5f;
        public const int BronzeMineDepthMin = 3;
        public const int BronzeMineDepthMax = 5;

        public const int SilverMinePrice = 800;
        public const float SilverMineSellBackRatio = 0.5f;
        public const int SilverMineDepthMin = 5;
        public const int SilverMineDepthMax = 9;

        public const int GoldMinePrice = 2000;
        public const float GoldMineSellBackRatio = 0.5f;
        public const int GoldMineDepthMin = 10;
        public const int GoldMineDepthMax = 15;

        // Default pickaxe shop offers
        public const int StonePickaxePrice = 500;
        public const int StonePickaxePower = 2;
        public const int StonePickaxeRequiredLevel = 1;

        public const int IronPickaxePrice = 2000;
        public const int IronPickaxePower = 5;
        public const int IronPickaxeRequiredLevel = 3;

        public const int GoldPickaxePrice = 5000;
        public const int GoldPickaxePower = 10;
        public const int GoldPickaxeRequiredLevel = 6;

        public const int DiamondPickaxePrice = 15000;
        public const int DiamondPickaxePower = 25;
        public const int DiamondPickaxeRequiredLevel = 10;
    }
}
