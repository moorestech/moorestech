namespace Game.Gear.Common
{
    public class GearDemandSnapshotUpdater
    {
        private readonly GearDemandSnapshotStore _snapshotStore;

        public GearDemandSnapshotUpdater(GearDemandSnapshotStore snapshotStore)
        {
            _snapshotStore = snapshotStore;
        }

        public void Update(GearNetworkDatastore datastore)
        {
            // 変更されたnetworkだけ、現行互換の固定需要snapshotを再作成する。
            // Rebuild fixed compatibility demand snapshots only for changed networks.
            foreach (var network in datastore.GearNetworks.Values)
            {
                if (!network.RequiresDemandSnapshotRefresh) continue;
                foreach (var transformer in network.GearTransformers)
                {
                    _snapshotStore.SetDefaultDemand(transformer.BlockInstanceId);
                }
                network.MarkDemandSnapshotRefreshed();
            }
        }
    }
}
