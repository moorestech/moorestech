namespace Game.Gear.Common
{
    public class GearDemandSnapshotUpdater
    {
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly GearDemandSnapshotStore _snapshotStore;

        public GearDemandSnapshotUpdater(GearNetworkDatastore gearNetworkDatastore, GearDemandSnapshotStore snapshotStore)
        {
            _gearNetworkDatastore = gearNetworkDatastore;
            _snapshotStore = snapshotStore;
        }

        public void Update()
        {
            _snapshotStore.Clear();

            // 今回は全consumerを固定需要として扱う。
            // This first pass treats every consumer as fixed demand.
            foreach (var network in _gearNetworkDatastore.GearNetworks.Values)
            {
                foreach (var transformer in network.GearTransformers)
                {
                    _snapshotStore.SetSnapshot(GearDemandSnapshot.Enabled(transformer.BlockInstanceId));
                }
            }
        }
    }
}
