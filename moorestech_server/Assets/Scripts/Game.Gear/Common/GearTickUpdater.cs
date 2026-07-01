namespace Game.Gear.Common
{
    public class GearTickUpdater
    {
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly GearDemandSnapshotUpdater _demandSnapshotUpdater;
        private readonly GearDemandSnapshotStore _demandSnapshotStore;
        private readonly GearRuntimeStateStore _runtimeStateStore;

        public GearTickUpdater(
            GearNetworkDatastore gearNetworkDatastore,
            GearDemandSnapshotUpdater demandSnapshotUpdater,
            GearDemandSnapshotStore demandSnapshotStore,
            GearRuntimeStateStore runtimeStateStore)
        {
            _gearNetworkDatastore = gearNetworkDatastore;
            _demandSnapshotUpdater = demandSnapshotUpdater;
            _demandSnapshotStore = demandSnapshotStore;
            _runtimeStateStore = runtimeStateStore;
        }

        public void Update()
        {
            // 需要snapshotを先に確定し、network計算の入力を固定する。
            // First freeze demand snapshots so network calculation has stable input.
            _demandSnapshotUpdater.Update();

            // runtime stateはtick結果なので毎tick作り直す。
            // Runtime state represents this tick result, so rebuild it every tick.
            _runtimeStateStore.Clear();

            // topology済みnetworkを列挙し、需給と燃料消費まで一括実行する。
            // Walk finalized networks and execute balance, supply, and fuel consumption together.
            _gearNetworkDatastore.UpdateAllNetworks(_demandSnapshotStore, _runtimeStateStore);
        }
    }
}
