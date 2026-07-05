namespace Game.Gear.Tick
{
    // 毎tickの需要snapshot生成を担う。現状は固定需要のため同一インスタンスを返す
    // Produces the demand snapshot each tick; returns a cached instance while demand is fixed
    public class GearDemandSnapshotUpdater
    {
        // ManualUpdate等、tick外から需給計算する際に使う共有snapshot
        // Shared snapshot used when recalculating outside the tick loop (e.g. ManualUpdate)
        public static readonly GearDemandSnapshot SharedSnapshot = new();

        private readonly GearDemandSnapshot _snapshot = new();

        public GearDemandSnapshot UpdateSnapshot()
        {
            // 将来consumerごとの動的需要を集計する場合はここで再構築する
            // When dynamic per-consumer demand arrives, rebuild the snapshot here
            return _snapshot;
        }
    }
}
