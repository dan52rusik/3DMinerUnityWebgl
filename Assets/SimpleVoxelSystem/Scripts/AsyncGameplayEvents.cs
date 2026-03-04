using System;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public enum AsyncGameplayEventType
    {
        MineBlock,
        SellBackpack,
        BuyMine,
        PlaceMine,
        SellMine,
        WorldSwitch
    }

    public struct AsyncGameplayEvent
    {
        public AsyncGameplayEventType Type;
        public int gx;
        public int gy;
        public int gz;
        public int moneyDelta;
        public int xpDelta;
        public int miningLevel;
        public bool inLobby;
        public int blockType;
        public int mineDepth;
        public string mineName;
    }

    public static class AsyncGameplayEvents
    {
        public static Action<AsyncGameplayEvent> OnEvent;

        public static void PublishMineBlock(int gx, int gy, int gz, BlockType blockType, int xp, bool inLobby)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.MineBlock,
                gx = gx,
                gy = gy,
                gz = gz,
                xpDelta = xp,
                inLobby = inLobby,
                blockType = (int)blockType,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishSellBackpack(int moneyDelta)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.SellBackpack,
                moneyDelta = moneyDelta,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishBuyMine(string mineName, int mineDepth, int moneyDelta)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.BuyMine,
                mineName = mineName ?? string.Empty,
                mineDepth = mineDepth,
                moneyDelta = moneyDelta,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishPlaceMine(string mineName, int gx, int gz)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.PlaceMine,
                mineName = mineName ?? string.Empty,
                gx = gx,
                gz = gz,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishSellMine(string mineName, int moneyDelta)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.SellMine,
                mineName = mineName ?? string.Empty,
                moneyDelta = moneyDelta,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }

        public static void PublishWorldSwitch(bool inLobby)
        {
            OnEvent?.Invoke(new AsyncGameplayEvent
            {
                Type = AsyncGameplayEventType.WorldSwitch,
                inLobby = inLobby,
                miningLevel = GlobalEconomy.MiningLevel
            });
        }
    }
}
