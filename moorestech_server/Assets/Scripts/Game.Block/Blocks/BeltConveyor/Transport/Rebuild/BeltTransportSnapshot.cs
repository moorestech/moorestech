using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 再構築の直前に、旧構成のアイテムをマス・buffer・内部segmentごとに取り出したもの。取り出しは旧segmentを変更しない
    // Items of the old assembly taken right before a rebuild, grouped by cell, buffer and internal segment; capturing leaves the old segments untouched
    // ロードした保存内容も同じ記録として足され、同じ復元手順に乗る
    // Loaded save content is added as the same kind of records and goes through the same restore procedure
    public sealed class BeltTransportSnapshot
    {
        public readonly List<BeltRunningItemRecord> RunningItems = new();
        public readonly List<BeltBufferItemRecord> BufferItems = new();
        public readonly List<BeltInternalItemRecord> InternalItems = new();
        // 保存内容が読めなかったblock。そのblockを含むsegmentにはアイテムを1つも復元しない
        // Blocks whose saved content was unreadable; no item is restored into a segment containing one
        public readonly HashSet<BlockInstanceId> CorruptedBlocks = new();
        // 合流・分岐の現在の優先順を、優先順が属するblockで持つ。セーブの切り出しだけが使い、再構築はトポロジー変更で初期化する仕様なので引き継がない
        // Current merge and branch priority orders keyed by the block they belong to; only save extraction uses them, since a topology rebuild resets orders by spec
        public readonly Dictionary<BlockInstanceId, int> PriorityOrders = new();

        public static BeltTransportSnapshot Capture(BeltTransportAssembly assembly)
        {
            var snapshot = new BeltTransportSnapshot();
            var layouts = assembly.Layouts;
            for (var i = 0; i < layouts.Length; i++)
            {
                var layout = layouts[i];
                var segment = assembly.Segments[i];
                if (layout.IsInternal)
                {
                    CaptureInternal(layout, segment);
                    continue;
                }
                CaptureRunning(i, layout, segment);
                if (segment is BeltBufferedSegment buffered) CaptureBuffered(i, layout, buffered);
            }
            return snapshot;

            #region Internal

            void CaptureRunning(int segmentIndex, BeltSegmentLayout layout, BeltConveyorSegment segment)
            {
                // 先頭は常にsegment内にあるので、出口までの距離からマスとマス内の残り距離が決まる
                // The head is always inside the segment, so the distance to the exit yields the cell and the remaining distance within it
                var states = segment.CaptureItems();
                var cells = layout.Cells;
                for (var order = 0; order < states.Length; order++)
                {
                    var cellIndex = BeltCellLocation.CellIndexOf(cells.Length, states[order].DistanceToExit, out var distanceToCellExit);
                    snapshot.RunningItems.Add(new BeltRunningItemRecord(cells[cellIndex].BlockInstanceId, distanceToCellExit, states[order].Item, segmentIndex, order));
                }
            }

            void CaptureBuffered(int segmentIndex, BeltSegmentLayout layout, BeltBufferedSegment buffered)
            {
                // 合流・分岐のbufferと優先順は末尾マスのblockに属する
                // The buffer and priority order of a merge or branch belong to the last cell's block
                var lastCellBlock = layout.Cells[layout.Cells.Length - 1].BlockInstanceId;
                snapshot.PriorityOrders.Add(lastCellBlock, buffered.PriorityOrder);
                if (buffered.Buffer.TryGetItem(out var held)) snapshot.BufferItems.Add(new BeltBufferItemRecord(lastCellBlock, held, segmentIndex));
            }

            void CaptureInternal(BeltSegmentLayout layout, BeltConveyorSegment segment)
            {
                // 内部segmentは合流へ出す唯一の出力を持ち、その合流の先頭マスが所有block
                // An internal segment has its single output into a merge, whose head cell is the owning block
                var states = segment.CaptureItems();
                if (states.Length == 0) return;
                var merge = layouts[layout.Outputs[0].PartnerSegmentIndex];
                snapshot.InternalItems.Add(new BeltInternalItemRecord(merge.Cells[0].BlockInstanceId, layout.Inputs[0].Direction, states));
            }

            #endregion
        }
    }
}
