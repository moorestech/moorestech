using System;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ワールド全体のベルト搬送の1組を持つ。設置・撤去でdirtyになり、tick先頭のRebuildIfDirtyでワールドから作り直す（流体・歯車と同型）
    // Holds the single world-wide belt transport assembly; placement and removal mark it dirty and RebuildIfDirty at the tick head rebuilds it from the world, like fluid and gear
    // 再構築では旧構成のアイテムを取り出し、撤去と再構築の仕様に従って新構成へ復元する。置けないアイテムは消滅する
    // A rebuild captures the old assembly's items and restores them into the new one per the removal/rebuild spec; items that cannot be placed vanish
    public class BeltTransportDatastore
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private bool _isTopologyDirty = true;

        public BeltTransportAssembly Assembly { get; private set; }

        public BeltTransportDatastore(IWorldBlockDatastore worldBlockDatastore, IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
            Assembly = BeltTransportAssembler.Assemble(Array.Empty<BeltSegmentLayout>());
            worldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(_ => _isTopologyDirty = true);
            worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(_ => _isTopologyDirty = true);
        }

        // tick先頭にMasterTickUpdaterから呼ばれ、設置・撤去があった時だけワールドから組を作り直す
        // Called by MasterTickUpdater at the tick head; rebuilds the assembly from the world only after a placement or removal
        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;
            _isTopologyDirty = false;
            var snapshot = BeltTransportSnapshot.Capture(Assembly);
            Assembly = BeltTransportAssembler.Assemble(BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(_worldBlockDatastore)));
            BeltTransportRestorer.Restore(snapshot, Assembly);
        }
    }
}
