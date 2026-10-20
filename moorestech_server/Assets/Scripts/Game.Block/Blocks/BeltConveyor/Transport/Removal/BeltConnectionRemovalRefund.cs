using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    internal static class BeltConnectionRemovalRefund
    {
        internal static IReadOnlyList<IItemStack> GetItems(IBlock block, IWorldBlockDatastore world,
            BeltTransportAssembly assembly, IReadOnlyDictionary<BlockInstanceId, BeltConveyorSaveJsonObject> loadedStates)
        {
            var removedInputs = GetRemovedInputs(block, world);
            var items = new List<IItemStack>();
            if (removedInputs.Count == 0) return items;

            // pending所有者は実状態と重ねず検証する
            // Validate pending owners without overlapping their live state
            var snapshot = BeltTransportSnapshot.Capture(assembly);
            snapshot.InternalItems.RemoveAll(record => loadedStates.ContainsKey(record.MergeBlockInstanceId));
            var pending = new Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject>();
            foreach (var key in removedInputs)
                if (loadedStates.TryGetValue(key.MergeBlock, out var state)) pending[key.MergeBlock] = state;
            BeltTransportLoadedStateConverter.Convert(pending, snapshot);

            // 実状態の個体IDを保って返却する
            // Preserve live item identities in the refund
            foreach (var record in snapshot.InternalItems)
            {
                if (!removedInputs.Contains((record.MergeBlockInstanceId, record.InputDirection))) continue;
                foreach (var state in record.States)
                    items.Add(ServerContext.ItemStackFactory.Create(state.Item.ItemId, 1, state.Item.ItemInstanceId));
            }
            return items;
        }

        internal static void Consume(IBlock block, IWorldBlockDatastore world, BeltTransportAssembly assembly,
            Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> loadedStates)
        {
            var removedInputs = GetRemovedInputs(block, world);
            if (removedInputs.Count == 0) return;

            // 古い構成にも同じキーを適用して二重返却を防ぐ
            // Apply the same keys to the old assembly to prevent duplicate refunds
            foreach (var layout in assembly.Layouts)
            {
                if (!layout.IsInternal) continue;
                var merge = assembly.Layouts[layout.Outputs[0].PartnerSegmentIndex];
                if (removedInputs.Contains((merge.Cells[0].BlockInstanceId, layout.Inputs[0].Direction)))
                    assembly.Segments[layout.Index].RestoreItems(Array.Empty<BeltItemState>());
            }

            // 保存入力を変更せず対象方向だけ除いた内容へ置換する
            // Replace pending content without mutating its input object or unrelated fields
            var owners = new HashSet<BlockInstanceId>();
            foreach (var key in removedInputs) owners.Add(key.MergeBlock);
            foreach (var owner in owners)
            {
                if (!loadedStates.TryGetValue(owner, out var pending)) continue;
                var snapshot = new BeltTransportSnapshot();
                BeltTransportLoadedStateConverter.Convert(new Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> { [owner] = pending }, snapshot);
                if (snapshot.CorruptedBlocks.Contains(owner)) continue;
                var remaining = new List<BeltInternalItemSaveJsonObject>();
                foreach (var saved in pending.InternalItems)
                    if (!removedInputs.Contains((owner, (BeltDirection)saved.InputDirection))) remaining.Add(saved);
                loadedStates[owner] = new BeltConveyorSaveJsonObject
                {
                    Items = pending.Items, BufferItem = pending.BufferItem,
                    PriorityOrder = pending.PriorityOrder, InternalItems = remaining
                };
            }
        }

        private static HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)> GetRemovedInputs(IBlock block, IWorldBlockDatastore world)
        {
            var removedInputs = new HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)>();
            if (block.BlockMasterElement.BlockParam is IBeltSpeedParam) return removedInputs;

            // 同tickの先行撤去を反映した現在の接続を読む
            // Read current connections including earlier removals in the same tick
            var layouts = BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(world));
            foreach (var layout in layouts)
            {
                if (!layout.IsInternal) continue;
                var merge = layouts[layout.Outputs[0].PartnerSegmentIndex];
                var survivingInputs = 0;
                var removesInput = false;
                foreach (var input in merge.Inputs)
                {
                    if (input.Connection.PartnerBlock.BlockInstanceId == block.BlockInstanceId) removesInput = true;
                    else survivingInputs++;
                }

                // 合流解消時は残る入力側の内部区間も消える
                // Dissolving a merge also removes internal segments on surviving inputs
                if (removesInput && (survivingInputs < 2 || layout.Inputs[0].Connection.PartnerBlock.BlockInstanceId == block.BlockInstanceId))
                    removedInputs.Add((merge.Cells[0].BlockInstanceId, layout.Inputs[0].Direction));
            }
            return removedInputs;
        }
    }
}
