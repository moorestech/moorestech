using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    // ベルコンblockが所有するアイテムを返却物にする。所有の規則は再構築・セーブと同じ(走行中は先頭のマス、bufferは末尾マス、内部segmentは合流マス)
    // Turns the items a belt block owns into refunds; ownership follows the rebuild and save rules (running: head cell, buffer: last cell, internal segment: merge cell)
    // 一括撤去は撤去1個ごとにここを通るので、組の全量は写さず、そのblockが属するsegmentと自分の内部segmentだけを読む
    // A bulk removal passes here once per block, so this reads only the block's own segment and its internal segments instead of capturing the whole assembly
    internal static class BeltOwnedItemRefund
    {
        private static readonly BeltDirection[] InputDirections = { BeltDirection.Front, BeltDirection.Back, BeltDirection.Left, BeltDirection.Right };

        internal static IReadOnlyList<IItemStack> GetItems(BlockInstanceId blockInstanceId, BeltTransportAssembly assembly)
        {
            var items = new List<IItemStack>();
            // 組に居ないblock(設置した同tickで未再構築)は何も所有していない
            // A block absent from the assembly (placed this tick, not rebuilt yet) owns nothing
            if (!assembly.Locator.TryLocate(blockInstanceId, out var location)) return items;

            AddRunningItems();
            AddBufferItem();
            AddInternalItems();
            return items;

            #region Internal

            void AddRunningItems()
            {
                // 出口に近い順に並ぶので、このマスの範囲を過ぎたら打ち切る
                // Items come in exit order, so stop once past this cell's range
                var cellEnd = location.CellExitDistance + BeltConstants.ItemWidth;
                foreach (var state in assembly.Segments[location.SegmentIndex].EnumerateItems())
                {
                    if (cellEnd <= state.DistanceToExit) break;
                    if (location.CellExitDistance <= state.DistanceToExit) Add(state.Item);
                }
            }

            void AddBufferItem()
            {
                // 合流・分岐のbufferは末尾マスのblockが持つ
                // A merge or branch buffer belongs to the last cell's block
                if (!location.IsLastCell || assembly.Segments[location.SegmentIndex] is not BeltBufferedSegment buffered) return;
                if (buffered.Buffer.TryGetItem(out var held)) Add(held);
            }

            void AddInternalItems()
            {
                // このblockを合流マスとする内部segmentを、全量の列挙と同じsegment番号順で返す
                // Internal segments merging into this block, in the same segment-index order as a full capture
                var internalIndices = new List<int>();
                foreach (var direction in InputDirections)
                    if (assembly.Locator.TryFindInternal(blockInstanceId, direction, out var segmentIndex)) internalIndices.Add(segmentIndex);
                internalIndices.Sort();
                foreach (var segmentIndex in internalIndices)
                    foreach (var state in assembly.Segments[segmentIndex].EnumerateItems()) Add(state.Item);
            }

            void Add(in BeltItem item)
            {
                items.Add(ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId));
            }

            #endregion
        }
    }
}
