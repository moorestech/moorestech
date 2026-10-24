using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Blocks.BeltConveyor.Transport.Removal;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // ワールド全体のベルト搬送の1組を持つ。ベルト・搬入出できるブロックの設置・撤去でdirtyになり、tick末尾のRebuildIfDirtyでワールドから作り直す
    // Holds the single world-wide belt transport assembly; placing or removing a belt or an item-connected block marks it dirty and RebuildIfDirty at the tick end rebuilds it from the world
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
        private readonly Subject<Unit> _onAssemblyRebuilt = new();
        private bool _isTopologyDirty = true;
        // 搬送tick後〜次tick先頭の受付開始前。この区間の押し込みは境界ハッシュに載るが差分に載らないため拒否する
        // From the transport tick until the window opens at the next tick head; a push here would land in the boundary hash but in no diff, so it is refused
        private bool _isAfterTransportTick;
        // セーブ1回分の切り出し結果。組を変える操作(搬送tick・再構築・押し込み)で捨てる
        // Save content cut out for one save pass; dropped by every operation that changes the assembly (transport tick, rebuild, push)
        private Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> _capturedSaveStates;

        // 組そのものは可変なのでinterfaceには出さない
        // The assembly itself is mutable and is therefore exposed through neither interface
        public BeltTransportAssembly Assembly { get; private set; }

        public IObservable<Unit> OnAssemblyRebuilt => _onAssemblyRebuilt;

        public BeltTransportDatastore(IWorldBlockDatastore worldBlockDatastore, IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
            Assembly = BeltTransportAssembler.Assemble(Array.Empty<BeltSegmentLayout>(), new Dictionary<BlockInstanceId, int>(), _diffRecorder);

            // 流体・歯車は全ブロックの設置・撤去で作り直すが、ベルトは作り直すと合流・分岐の優先順が初期化されるので構成に関わるブロックだけを契機にする
            // Fluid and gear rebuild on every placement or removal, but a belt rebuild resets merge and branch priority orders, so only blocks that affect the layout trigger it
            worldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties => MarkDirtyIfAffectsLayout(properties.BlockData.Block));
            worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => MarkDirtyIfAffectsLayout(properties.BlockData.Block));
        }

        // tick先頭にMasterTickUpdaterから呼ばれ、ここから搬送tickまで(block更新)の押し込みを受け付ける
        // Called by MasterTickUpdater at the tick head; pushes are accepted from here until the transport tick (block updates)
        public void BeginMachinePushWindow()
        {
            _isAfterTransportTick = false;
        }

        // tick末尾の操作適用後とロード完了時に呼ばれ、設置・撤去・ロードがあった時だけワールドから組を作り直す。押し込みの受付区間は開かない
        // Called after tick-end input and on load completion; rebuilds the assembly from the world only after a placement, removal or load, and never opens the push window
        public void RebuildIfDirty()
        {
            if (!_isTopologyDirty) return;
            _isTopologyDirty = false;
            var snapshot = BeltTransportSnapshot.Capture(Assembly);
            var savedPriorityOrders = BeltTransportLoadedStateConverter.Convert(_loadedStates, snapshot);
            _loadedStates.Clear();
            Assembly = BeltTransportAssembler.Assemble(BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(_worldBlockDatastore)), savedPriorityOrders, _diffRecorder);
            BeltTransportRestorer.Restore(snapshot, Assembly);
            _capturedSaveStates = null;

            // 境界ハッシュより前に全量を送らせる。クライアントの複製は差分でなくこの全量で組み直す
            // Have the full state sent before the boundary hash; the client replica re-assembles from it rather than from a diff
            _onAssemblyRebuilt.OnNext(Unit.Default);
        }

        // 組を1tick進め、そのtickで成立した差分を報告する。搬送と差分の取り出しを分けると差分が次tickへ混ざる
        // Advances the assembly one tick and reports the diff settled in it; splitting transport from taking the diff would leak it into the next tick
        public BeltTransportTickReport Tick()
        {
            Assembly.ApplyGearDriveSpeeds();
            Assembly.Simulation.Tick();
            var report = new BeltTransportTickReport(_diffRecorder.TakeTickDiff());
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

        // 現在の組のハッシュ。クライアントの複製との照合に使う。全量を写さず直接混ぜる
        // Hash of the current assembly, checked against the client replica; mixes directly without capturing the full state
        public uint ComputeStateHash()
        {
            return BeltTransportStateHash.Compute(Assembly.Shapes, Assembly.Segments);
        }

        // ロードしたベルコンblockの保存内容を預かる。次の再構築で復元手順に乗せる
        // Takes a loaded belt block's save content; the next rebuild runs it through the restore procedure
        public void RegisterLoadedState(BlockInstanceId blockInstanceId, BeltConveyorSaveJsonObject state)
        {
            _loadedStates[blockInstanceId] = state;
            _isTopologyDirty = true;
        }

        // blockの保存内容。セーブは全blockを順に問い合わせるので、組を1回だけ取り出して全blockへ振り分けた結果を次の変更まで使い回す
        // The block's save content; a save queries every block in turn, so the assembly is captured once, distributed to every block, and reused until the next change
        public BeltConveyorSaveJsonObject CreateSaveState(BlockInstanceId blockInstanceId)
        {
            _capturedSaveStates ??= BeltTransportSaveStateBuilder.Build(BeltTransportSnapshot.Capture(Assembly));

            // アイテムも優先順も持たないblockは空
            // A block holding no item or order saves as empty
            return _capturedSaveStates.TryGetValue(blockInstanceId, out var captured) ? captured : new BeltConveyorSaveJsonObject();
        }

        // blockが所有するアイテム(走行中・buffer・合流へ入る内部segment)を、組を変えずに返却物として列挙する
        // Lists the items a block owns (running, buffered, internal segments into its merge) as refunds without changing the assembly
        public IReadOnlyList<IItemStack> GetOwnedRefundItems(BlockInstanceId blockInstanceId)
        {
            return BeltOwnedItemRefund.GetItems(blockInstanceId, BeltTransportSnapshot.Capture(Assembly));
        }

        // 搬出元の撤去で消える内部segmentの中身を、組を変えずに返却物として列挙する
        // Lists, without changing the assembly, the contents of internal segments that removing a source would make vanish
        public IReadOnlyList<IItemStack> GetConnectionRemovalRefundItems(IBlock block)
        {
            return BeltConnectionRemovalRefund.GetItems(BeltConnectionRemovalRefund.GetRemovedInputs(block, _worldBlockDatastore), BeltTransportSnapshot.Capture(Assembly));
        }

        // 返却を確定した撤去の直前に呼び、消える内部segmentを空にする。形は再構築まで変えないので、同tickの後続の撤去・再設置・再構築の復元にこの中身が二度出てこない
        // Called right before a refunded removal; empties the vanishing internal segments while keeping the shape until the rebuild, so later removals, re-placements and the rebuild's restore in the same tick never see these items again
        public void DiscardConnectionRemovalItems(IBlock block)
        {
            foreach (var (mergeBlockInstanceId, inputDirection) in BeltConnectionRemovalRefund.GetRemovedInputs(block, _worldBlockDatastore))
            {
                if (!Assembly.Locator.TryFindInternal(mergeBlockInstanceId, inputDirection, out var segmentIndex)) continue;
                Assembly.Segments[segmentIndex].DiscardAllItems();
            }
            _capturedSaveStates = null;
        }

        private void MarkDirtyIfAffectsLayout(IBlock block)
        {
            // ベルト自身か、ベルトと搬入出で繋がりうるブロック(インベントリ接続を持つ)だけが構成を変える
            // Only a belt itself or a block that can exchange items with one (has an inventory connector) changes the layout
            if (block.BlockMasterElement.BlockParam is IBeltSpeedParam || block.ComponentManager.ExistsComponent<IBlockConnectorComponent<IBlockInventory>>()) _isTopologyDirty = true;
        }
    }
}
