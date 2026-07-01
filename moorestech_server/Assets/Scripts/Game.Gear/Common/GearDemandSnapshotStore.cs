using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public class GearDemandSnapshotStore
    {
        private static GearDemandSnapshotStore _instance;
        private readonly Dictionary<BlockInstanceId, GearDemandSnapshot> _snapshots = new();

        public GearDemandSnapshotStore()
        {
            _instance = this;
        }

        public static GearDemandSnapshotStore GetOrCreateForManualUpdate()
        {
            return _instance ?? new GearDemandSnapshotStore();
        }

        public void Clear()
        {
            _snapshots.Clear();
        }

        public void SetSnapshot(GearDemandSnapshot snapshot)
        {
            _snapshots[snapshot.BlockInstanceId] = snapshot;
        }

        public GearDemandSnapshot GetSnapshot(BlockInstanceId blockInstanceId)
        {
            if (_snapshots.TryGetValue(blockInstanceId, out var snapshot)) return snapshot;
            return GearDemandSnapshot.Enabled(blockInstanceId);
        }
    }
}
