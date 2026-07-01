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

        public void SetDefaultDemand(BlockInstanceId blockInstanceId)
        {
            _snapshots[blockInstanceId] = GearDemandSnapshot.CreateDefault(blockInstanceId);
        }

        public GearDemandSnapshot GetOrDefault(BlockInstanceId blockInstanceId)
        {
            if (_snapshots.TryGetValue(blockInstanceId, out var snapshot)) return snapshot;
            return GearDemandSnapshot.CreateDefault(blockInstanceId);
        }

        public void Remove(BlockInstanceId blockInstanceId)
        {
            _snapshots.Remove(blockInstanceId);
        }

        public static bool TryGetInstance(out GearDemandSnapshotStore store)
        {
            store = _instance;
            return store != null;
        }
    }
}
