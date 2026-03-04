namespace SimpleVoxelSystem
{
    /// <summary>
    /// Single source of truth for all economy tuning values.
    ///
    /// ── Progression curve (target: infinite grind, ~30-40 min per stage) ───
    ///
    ///  Stage 1 · Bronze Mine  (~2 runs, ~1 h total)
    ///    Start $500 → buy Bronze Mine ($350) → run ×1 → Stone Pickaxe ($600)
    ///    → run ×2 → Silver Mine ($3 000)
    ///
    ///  Stage 2 · Silver Mine  (~2 runs, ~1.5 h total)
    ///    → run ×1 → Iron Pickaxe ($5 000)
    ///    → run ×2 → Gold Mine ($10 000)
    ///
    ///  Stage 3 · Gold Mine  (~4 runs, ~3 h total)
    ///    → run ×2 → Gold Pickaxe ($18 000)
    ///    → run ×4 → Diamond Pickaxe ($80 000)  ← infinite grind milestone
    ///
    ///  Block rewards (reward / XP per block):
    ///    Dirt  1 / 1    Stone  8 / 5    Iron  35 / 20    Gold  80 / 40
    ///  These are set in MiningManager.EnsureBasicBlockConfig().
    /// </summary>
    public static class EconomyTuning
    {
        // ── Starting state ───────────────────────────────────────────────────
        public const int StartMoney        = 500;
        public const int StartMiningXP     = 0;
        public const int StartMiningLevel  = 1;

        // ── XP progression ───────────────────────────────────────────────────
        // XP needed to reach next level = currentLevel × this multiplier
        public const int MiningXpPerLevelMultiplier = 150;

        // ── Player upgrades (cost grows × PlayerUpgradeCostMultiplier each time)
        public const int   PlayerStrengthUpgradeStartCost = 200;
        public const int   BackpackUpgradeStartCost       = 300;
        public const float PlayerUpgradeCostMultiplier    = 1.6f;
        public const int   BackpackCapacityPerUpgrade     = 5;

        // ── Minion economy ───────────────────────────────────────────────────
        public const int MinionPurchasePrice      = 5000;   // late-game automation
        public const int MinionStrengthUpgradeCost = 500;
        public const int MinionCapacityUpgradeCost = 500;
        public const int MinionCapacityPerUpgrade  = 5;

        // ── Land plot ────────────────────────────────────────────────────────
        public const int LandPlotPurchasePrice = 2000;

        // ── Mine sizes (shared defaults) ─────────────────────────────────────
        public const int DefaultMineWellWidth  = 5;
        public const int DefaultMineWellLength = 5;
        public const int DefaultMinePadding    = 3;

        // ── Bronze Mine  (Stage 1 entry, ~$2 000 per run at 65% clear) ───────
        public const int   BronzeMinePrice          = 350;
        public const float BronzeMineSellBackRatio  = 0.4f;
        public const int   BronzeMineDepthMin       = 3;
        public const int   BronzeMineDepthMax       = 5;

        // ── Silver Mine  (Stage 2, ~$10 000 per run) ─────────────────────────
        public const int   SilverMinePrice          = 3000;
        public const float SilverMineSellBackRatio  = 0.4f;
        public const int   SilverMineDepthMin       = 5;
        public const int   SilverMineDepthMax       = 9;

        // ── Gold Mine  (Stage 3, ~$30 000 per run) ───────────────────────────
        public const int   GoldMinePrice            = 10000;
        public const float GoldMineSellBackRatio    = 0.4f;
        public const int   GoldMineDepthMin         = 10;
        public const int   GoldMineDepthMax         = 15;

        // ── Pickaxes ─────────────────────────────────────────────────────────
        //   Stone   — buy after first Bronze run, unlocks at Lv.1
        public const int StonePickaxePrice         = 600;
        public const int StonePickaxePower         = 2;
        public const int StonePickaxeRequiredLevel = 1;

        //   Iron    — buy mid-Silver stage, unlocks at Lv.4
        public const int IronPickaxePrice          = 5000;
        public const int IronPickaxePower          = 5;
        public const int IronPickaxeRequiredLevel  = 4;

        //   Gold    — buy mid-Gold stage, unlocks at Lv.8
        public const int GoldPickaxePrice          = 18000;
        public const int GoldPickaxePower          = 10;
        public const int GoldPickaxeRequiredLevel  = 8;

        //   Diamond — infinite grind milestone, unlocks at Lv.12
        public const int DiamondPickaxePrice          = 80000;
        public const int DiamondPickaxePower          = 25;
        public const int DiamondPickaxeRequiredLevel  = 12;
    }
}
