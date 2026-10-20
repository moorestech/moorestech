using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ワールド全体のベルト搬送の1組を持つ。ベルト・搬入出できるブロックの設置・撤去でdirtyになり、tick先頭のRebuildIfDirtyでワールドから作り直す
    // Holds the single world-wide belt transport assembly; placing or removing a belt or an item-connected block marks it dirty and RebuildIfDirty at the tick head rebuilds it from the world
    // 再構築では旧構成のアイテムとロード済みの保存内容を取り出し、撤去と再構築の仕様に従って新構成へ復元する。置けないアイテムは消滅する
    // A rebuild captures the old assembly's items and the loaded save content, then restores them into the new one per the removal/rebuild spec; items that cannot be placed vanish
    public class BeltTransportDatastore : IBeltTransportLookup, IBeltTransportMutation
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        // ロードで預かり、次の再構築で載せるまで保持する保存内容
        // Save content handed over at load and kept until the next rebuild places it
        private readonly Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> _loadedStates = new();
        // 機械との搬送の成立を溜め、搬送tickの報告で取り出す
        // Accumulates settled machine handoffs, taken by the transport tick report
        private readonly BeltTransportDiffRecorder _diffRecorder = new();
        private bool _isTopologyDirty = true;
        // tick先頭の再構築を覚えておき、搬送後の報告に載せる
        // Remembers a rebuild at the tick head so the post-transport report carries it
        private bool _rebuiltSinceLastTick;
        // 搬送tick後〜次tick先頭のRebuildIfDirty前。この区間の押し込みは境界ハッシュに載るが差分に載らないため拒否する
        // From the transport tick to the next tick-head RebuildIfDirty; a push here would land in the boundary hash but in no diff, so it is refused
        private bool _isAfterTransportTick;
        // セーブ1回分の切り出し結果。組を変える操作(搬送tick・再構築・押し込み)で捨てる
        // Save content cut out for one save pass; dropped by every operation that changes the assembly (transport tick, rebuild, push)
        private Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> _capturedSaveStates;

        // 組そのものは可変なのでinterfaceには出さない
        // The assembly itself is mutable and is therefore exposed through neither interface
        public BeltTransportAssembly Assembly { get; private set; }

        public BeltTransportDatastore(IWorldBlockDatastore worldBlockDatastore, IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
            Assembly = BeltTransportAssembler.Assemble(Array.Empty<BeltSegmentLayout>(), new Dictionary<BlockInstanceId, int>(), _diffRecorder);

            // 流体・歯車は全ブロックの設置・撤去で作り直すが、ベルトは作り直すと合流・分岐の優先順が初期化されるので構成に関わるブロックだけを契機にする
            // Fluid and gear rebuild on every placement or removal, but a belt rebuild resets merge and branch priority orders, so only blocks that affect the layout trigger it
            worldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties => MarkDirtyIfAffectsLayout(properties.BlockData.Block));
            worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => MarkDirtyIfAffectsLayout(properties.BlockData.Block));
        }

        // tick先頭にMasterTickUpdaterから毎tick呼ばれ、設置・撤去・ロードがあった時だけワールドから組を作り直す
        // Called by MasterTickUpdater at every tick head; rebuilds the assembly from the world only after a placement, removal or load
        public void RebuildIfDirty()
        {
            // dirtyでなくても、ここから押し込みを受け付ける区間(block更新)に入る
            // Even when not dirty, the window that accepts pushes (block updates) opens here
            _isAfterTransportTick = false;
            if (!_isTopologyDirty) return;
            _isTopologyDirty = false;
            var snapshot = BeltTransportSnapshot.Capture(Assembly);
            var savedPriorityOrders = BeltTransportLoadedStateConverter.Convert(_loadedStates, snapshot);
            _loadedStates.Clear();
            Assembly = BeltTransportAssembler.Assemble(BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(_worldBlockDatastore)), savedPriorityOrders, _diffRecorder);
            BeltTransportRestorer.Restore(snapshot, Assembly);

            // 旧構成の番号で書かれた未取り出しの差分は新構成へ適用しない。中身は再構築後の全量に含まれる
            // Untaken diffs written with old-assembly numbers are never applied to the new one; their effect is in the post-rebuild full state
            _diffRecorder.Discard();
            _capturedSaveStates = null;
            _rebuiltSinceLastTick = true;
        }

        // 組を1tick進め、そのtickの報告(先頭で作り直したか・成立した差分)を返す。搬送と差分の取り出しを分けると差分が次tickへ混ざる
        // Advances the assembly one tick and returns that tick's report (rebuilt at the head, settled diff); splitting transport from taking the diff would leak it into the next tick
        public BeltTransportTickReport Tick()
        {
            Assembly.ApplyGearDriveSpeeds();
            Assembly.Simulation.Tick();
            var report = new BeltTransportTickReport(_rebuiltSinceLastTick, _diffRecorder.TakeTickDiff());
            _rebuiltSinceLastTick = false;
            _capturedSaveStates = null;
            _isAfterTransportTick = true;
            return report;
        }

        // 機械の押し込みを組の受け口へ渡す。block更新中(再構築後〜搬送tick前)だけ受け付ける
        // Hands a machine push to the assembly's port; accepted only during block updates (after the rebuild, before the transport tick)
        public bool TrySupplyFromMachine(BlockInstanceId beltBlockInstanceId, in InsertItemContext context, ItemId itemId, ItemInstanceId itemInstanceId)
        {
            if (_isAfterTransportTick)
            {
                // 搬送後の押し込みは複製へ届かずハッシュ不一致で全クライアントが止まる。呼び出し経路の誤りなので拒否して残す
                // A post-transport push never reaches the replica and stops every client on a hash mismatch; it is a caller bug, so refuse and report it
                Debug.LogError($"[BeltTransport] push into belt block {beltBlockInstanceId} from {context.SourceBlockInstanceId} arrived after the transport tick and before the next rebuild; refused.");
                return false;
            }
            if (!Assembly.TrySupplyFromMachine(beltBlockInstanceId, context, itemId, itemInstanceId)) return false;
            _capturedSaveStates = null;
            return true;
        }

        // 現在の組の全量を切り出す。初回同期・再構築時の全量送信が使う
        // Cuts the current assembly's full state; used for the initial sync and the full-state send after a rebuild
        public BeltTransportFullState CaptureFullState()
        {
            return BeltTransportFullStateCapture.Capture(Assembly.Shapes, Assembly.Segments);
        }

        // 現在の組の全量ハッシュ。クライアントの複製との照合に使う
        // Full-state hash of the current assembly, checked against the client replica
        public uint ComputeStateHash()
        {
            return BeltTransportStateHash.Compute(CaptureFullState());
        }

        // ロードしたベルコンblockの保存内容を預かる。次の再構築で復元手順に乗せる
        // Takes a loaded belt block's save content; the next rebuild runs it through the restore procedure
        public void RegisterLoadedState(BlockInstanceId blockInstanceId, BeltConveyorSaveJsonObject state)
        {
            _loadedStates[blockInstanceId] = state;
            _isTopologyDirty = true;
        }

        // blockの保存内容。ロード後まだ載せていないblockは預かった内容をそのまま返す(預かり後は書き換えない)
        // The block's save content; a loaded block not yet placed returns the content it handed over (never modified after that)
        // セーブは全blockを順に問い合わせるので、組を1回だけ取り出して全blockへ振り分けた結果を次の変更まで使い回す
        // A save queries every block in turn, so the assembly is captured once, distributed to every block, and reused until the next change
        public BeltConveyorSaveJsonObject CreateSaveState(BlockInstanceId blockInstanceId)
        {
            if (_loadedStates.TryGetValue(blockInstanceId, out var pending)) return pending;
            _capturedSaveStates ??= BeltTransportSaveStateBuilder.Build(BeltTransportSnapshot.Capture(Assembly));

            // 設置直後で組にまだ無いblock、アイテムも優先順も持たないblockは空
            // A block placed this tick and not yet in the assembly, or one holding no item or order, saves as empty
            return _capturedSaveStates.TryGetValue(blockInstanceId, out var captured) ? captured : new BeltConveyorSaveJsonObject();
        }

        public IReadOnlyList<IItemStack> GetRefundItems(BlockInstanceId blockInstanceId)
        {
            // pendingは組と重ねず読む
            // Read pending state without overlapping the assembly
            BeltTransportSnapshot snapshot;
            if (_loadedStates.TryGetValue(blockInstanceId, out var pending))
            {
                snapshot = new BeltTransportSnapshot();
                BeltTransportLoadedStateConverter.Convert(new Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> { [blockInstanceId] = pending }, snapshot);
            }
            else snapshot = BeltTransportSnapshot.Capture(Assembly);

            // 再構築と同じ所有規則で現在の品を集める
            // Collect current items using the rebuild ownership rules
            var items = new List<IItemStack>();
            foreach (var record in snapshot.RunningItems) Add(record.CellBlockInstanceId, record.Item);
            foreach (var record in snapshot.BufferItems) Add(record.CellBlockInstanceId, record.Item);
            foreach (var record in snapshot.InternalItems)
                foreach (var state in record.States) Add(record.MergeBlockInstanceId, state.Item);
            return items;

            #region Internal
            void Add(BlockInstanceId owner, BeltItem item)
            {
                if (owner == blockInstanceId)
                    items.Add(ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId));
            }
            #endregion
        }

        private void MarkDirtyIfAffectsLayout(IBlock block)
        {
            // ベルト自身か、ベルトと搬入出で繋がりうるブロック(インベントリ接続を持つ)だけが構成を変える
            // Only a belt itself or a block that can exchange items with one (has an inventory connector) changes the layout
            if (block.BlockMasterElement.BlockParam is IBeltSpeedParam || block.ComponentManager.ExistsComponent<IBlockConnectorComponent<IBlockInventory>>()) _isTopologyDirty = true;
        }
    }
}
