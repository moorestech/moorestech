using System;
using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ワールド全体のベルト搬送の1組を持つ。設置・撤去でdirtyになり、tick先頭のRebuildIfDirtyでワールドから作り直す（流体・歯車と同型）
    // Holds the single world-wide belt transport assembly; placement and removal mark it dirty and RebuildIfDirty at the tick head rebuilds it from the world, like fluid and gear
    // 再構築では旧構成のアイテムとロード済みの保存内容を取り出し、撤去と再構築の仕様に従って新構成へ復元する。置けないアイテムは消滅する
    // A rebuild captures the old assembly's items and the loaded save content, then restores them into the new one per the removal/rebuild spec; items that cannot be placed vanish
    public class BeltTransportDatastore
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        // ロードで預かり、次の再構築で載せるまで保持する保存内容
        // Save content handed over at load and kept until the next rebuild places it
        private readonly Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> _loadedStates = new();
        private bool _isTopologyDirty = true;

        public BeltTransportAssembly Assembly { get; private set; }
        // 機械との搬送の成立を溜める。tickの束を作る側が搬送tick直後に取り出す
        // Accumulates settled machine handoffs; the tick-bundle sender takes them right after the transport tick
        public BeltTransportDiffRecorder DiffRecorder { get; } = new();

        public BeltTransportDatastore(IWorldBlockDatastore worldBlockDatastore, IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
            Assembly = BeltTransportAssembler.Assemble(Array.Empty<BeltSegmentLayout>(), new Dictionary<BlockInstanceId, int>(), DiffRecorder);
            worldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(_ => _isTopologyDirty = true);
            worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(_ => _isTopologyDirty = true);
        }

        // tick先頭にMasterTickUpdaterから呼ばれ、設置・撤去・ロードがあった時だけワールドから組を作り直す
        // Called by MasterTickUpdater at the tick head; rebuilds the assembly from the world only after a placement, removal or load
        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;
            _isTopologyDirty = false;
            var snapshot = BeltTransportSnapshot.Capture(Assembly);
            var savedPriorityOrders = BeltTransportLoadedStateConverter.Convert(_loadedStates, snapshot);
            _loadedStates.Clear();
            Assembly = BeltTransportAssembler.Assemble(BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(_worldBlockDatastore)), savedPriorityOrders, DiffRecorder);
            BeltTransportRestorer.Restore(snapshot, Assembly);

            // 旧構成の番号で書かれた未取り出しの差分は新構成へ適用しない。中身は再構築後の全量に含まれる
            // Untaken diffs written with old-assembly numbers are never applied to the new one; their effect is in the post-rebuild full state
            DiffRecorder.Discard();
        }

        // ロードしたベルコンblockの保存内容を預かる。次の再構築で復元手順に乗せる
        // Takes a loaded belt block's save content; the next rebuild runs it through the restore procedure
        public void RegisterLoadedState(BlockInstanceId blockInstanceId, BeltConveyorSaveJsonObject state)
        {
            _loadedStates[blockInstanceId] = state;
            _isTopologyDirty = true;
        }

        // blockの保存内容を組から切り出す。ロード後まだ載せていないblockは預かった内容をそのまま返す(預かり後は書き換えない)
        // Cuts the block's save content out of the assembly; a loaded block not yet placed returns the content it handed over (never modified after that)
        public BeltConveyorSaveJsonObject CreateSaveState(BlockInstanceId blockInstanceId)
        {
            return _loadedStates.TryGetValue(blockInstanceId, out var pending) ? pending : BeltTransportSaveStateBuilder.Build(Assembly, blockInstanceId);
        }
    }
}
