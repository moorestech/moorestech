using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 採掘中の品 1 つと毎分の採掘数
    /// One mined item and its per-minute mining rate
    /// </summary>
    public sealed class MinerMiningItem
    {
        public ItemId ItemId { get; }
        public double ItemsPerMinute { get; }

        public MinerMiningItem(ItemId itemId, double itemsPerMinute)
        {
            ItemId = itemId;
            ItemsPerMinute = itemsPerMinute;
        }
    }

    /// <summary>
    /// 採掘機の詳細の写し（電力・進捗・駆動・採掘中の品）
    /// Snapshot of a miner's detail (power, progress, drive and mined items)
    /// </summary>
    public sealed class MinerDetail
    {
        public double CurrentPower { get; }
        public double RequestPower { get; }
        public float Progress { get; }
        public bool GearDriven { get; }
        public IReadOnlyList<MinerMiningItem> MiningItems { get; }

        public MinerDetail(double currentPower, double requestPower, float progress, bool gearDriven, IReadOnlyList<MinerMiningItem> miningItems)
        {
            CurrentPower = currentPower;
            RequestPower = requestPower;
            Progress = progress;
            GearDriven = gearDriven;
            MiningItems = miningItems;
        }
    }
}
