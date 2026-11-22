using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    // ベルコン以外の搬出元(チェスト・機械・駅など)を撤去すると消える内部segmentを特定し、その中身を返却物にする
    // Identifies the internal segments that vanish when a non-belt source (chest, machine, station...) is removed, and turns their contents into refunds
    internal static class BeltConnectionRemovalRefund
    {
        internal static IReadOnlyList<IItemStack> GetItems(HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)> removedInputs, BeltTransportAssembly assembly)
        {
            var items = new List<IItemStack>();
            if (removedInputs.Count == 0) return items;

            // 消える内部segmentだけを引き、全量の列挙と同じsegment番号順に並べる
            // Look up only the vanishing internal segments, ordered by segment index like a full capture
            var internalIndices = new List<int>();
            foreach (var (mergeBlock, direction) in removedInputs)
                if (assembly.Locator.TryFindInternal(mergeBlock, direction, out var segmentIndex)) internalIndices.Add(segmentIndex);
            internalIndices.Sort();

            // 実状態の個体IDを保って返却する
            // Preserve live item identities in the refund
            foreach (var segmentIndex in internalIndices)
                foreach (var state in assembly.Segments[segmentIndex].EnumerateItems())
                    items.Add(ServerContext.ItemStackFactory.Create(state.Item.ItemId, 1, state.Item.ItemInstanceId));
            return items;
        }

        // ベルトへの出力を持つ、ベルコン以外の搬出元か。これが偽なら消える内部segmentは無く、ワールドの構成を調べる必要も無い
        // Whether the block is a non-belt source with an output into a belt; when false no internal segment vanishes and the world layout need not be inspected
        internal static bool IsSourceConnectedToBelt(IBlock block)
        {
            if (block.BlockMasterElement.BlockParam is IBeltSpeedParam) return false;
            if (!block.ComponentManager.TryGetComponent<IBlockConnectorComponent<IBlockInventory>>(out var connector)) return false;
            foreach (var info in connector.ConnectedTargets.Values)
                if (info.TargetBlock.BlockMasterElement.BlockParam is IBeltSpeedParam) return true;
            return false;
        }

        // 撤去前のワールドの構成から、blockを外すと消える内部segmentを合流マスと入力方向で返す
        // Returns, from the pre-removal world layout, the internal segments that removing the block makes vanish, keyed by merge cell and input direction
        internal static HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)> GetRemovedInputs(IBlock block, BeltSegmentLayout[] layouts)
        {
            var removedInputs = new HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)>();
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
