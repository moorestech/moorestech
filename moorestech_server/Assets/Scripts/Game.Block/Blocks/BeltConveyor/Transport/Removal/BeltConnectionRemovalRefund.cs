using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    // ベルコン以外の搬出元(チェスト・機械・駅など)を撤去すると消える内部segmentを特定し、その中身を返却物にする
    // Identifies the internal segments that vanish when a non-belt source (chest, machine, station...) is removed, and turns their contents into refunds
    internal static class BeltConnectionRemovalRefund
    {
        internal static IReadOnlyList<IItemStack> GetItems(HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)> removedInputs, BeltTransportSnapshot snapshot)
        {
            var items = new List<IItemStack>();
            if (removedInputs.Count == 0) return items;

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

        // 撤去前のワールドで、blockを外すと消える内部segmentを合流マスと入力方向で返す
        // Returns, from the world before removal, the internal segments that removing the block makes vanish, keyed by merge cell and input direction
        internal static HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)> GetRemovedInputs(IBlock block, IWorldBlockDatastore world)
        {
            var removedInputs = new HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)>();
            if (block.BlockMasterElement.BlockParam is IBeltSpeedParam) return removedInputs;

            // ベルトへの出力を持つ搬出元だけを調べる
            // Inspect only sources with an output connected to a belt
            if (!block.ComponentManager.TryGetComponent<IBlockConnectorComponent<IBlockInventory>>(out var connector)) return removedInputs;
            var hasBeltOutput = false;
            foreach (var info in connector.ConnectedTargets.Values)
                if (info.TargetBlock.BlockMasterElement.BlockParam is IBeltSpeedParam)
                {
                    hasBeltOutput = true;
                    break;
                }
            if (!hasBeltOutput) return removedInputs;

            // 同tickの先行撤去を反映した現在の接続を読む。組はtick末尾まで作り直さないので組の接続は古い
            // Read current connections including earlier removals in the same tick; the assembly is not rebuilt until the tick end, so its connections are stale
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
