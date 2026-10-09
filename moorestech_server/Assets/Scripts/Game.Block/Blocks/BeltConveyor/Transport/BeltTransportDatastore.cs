using System;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ワールド全体のベルト搬送の1組を持つ。設置・撤去でdirtyになり、tick先頭のRebuildIfDirtyでワールドから作り直す（流体・歯車と同型）
    // Holds the single world-wide belt transport assembly; placement and removal mark it dirty and RebuildIfDirty at the tick head rebuilds it from the world, like fluid and gear
    // 再構築では載っていたアイテムを引き継がない。復元の手順は撤去・再構築の仕様に従って別途載せる
    // A rebuild does not carry items over; restoration per the removal/rebuild spec is added separately
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
            Assembly = BeltTransportAssembler.Assemble(BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(_worldBlockDatastore)));
        }
    }
}
