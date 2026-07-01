namespace Game.Gear.Common
{
    public class GearTickUpdater
    {
        private readonly GearNetworkDatastore _networkDatastore;
        private readonly GearDemandSnapshotUpdater _demandSnapshotUpdater;
        private readonly GearDemandSnapshotStore _demandSnapshotStore;
        private readonly GearRuntimeStateStore _runtimeStateStore;

        public GearTickUpdater(
            GearNetworkDatastore networkDatastore,
            GearDemandSnapshotUpdater demandSnapshotUpdater,
            GearDemandSnapshotStore demandSnapshotStore,
            GearRuntimeStateStore runtimeStateStore)
        {
            _networkDatastore = networkDatastore;
            _demandSnapshotUpdater = demandSnapshotUpdater;
            _demandSnapshotStore = demandSnapshotStore;
            _runtimeStateStore = runtimeStateStore;
        }

        public void Update()
        {
            // topology変更をtick先頭で確定する。
            // Commit topology mutations at the start of the gear tick.
            _networkDatastore.ApplyTopologyMutations(_demandSnapshotStore, _runtimeStateStore);

            // 現行互換の固定需要snapshotをnetwork更新前に用意する。
            // Prepare compatibility demand snapshots before network solving.
            _demandSnapshotUpdater.Update(_networkDatastore);

            // 需給計算、runtime state反映、fuel generator消費をnetworkごとに実行する。
            // Solve supply-demand, write runtime state, and consume fuel per network.
            _networkDatastore.UpdateNetworks(_demandSnapshotStore, _runtimeStateStore);
        }
    }
}
