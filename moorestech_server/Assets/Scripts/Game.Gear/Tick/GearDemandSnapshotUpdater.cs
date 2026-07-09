namespace Game.Gear.Tick
{
    // tickごとの需要snapshotを生成し、需要固定中は同じインスタンスを返す
    // Produces the demand snapshot each tick; returns a cached instance while demand is fixed
    public class GearDemandSnapshotUpdater
    {
        private readonly GearDemandSnapshot _snapshot = new();

        public GearDemandSnapshot UpdateSnapshot()
        {
            // 将来consumerごとの動的需要を集計する場合はここで再構築する
            // Rebuild the snapshot here when dynamic per-consumer demand is introduced
            return _snapshot;
        }
    }
}
