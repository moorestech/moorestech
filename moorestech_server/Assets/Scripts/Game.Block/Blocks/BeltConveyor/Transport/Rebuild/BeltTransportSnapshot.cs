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
                if (segment is BeltBufferedSegment buffered && buffered.Buffer.TryGetItem(out var held))
                    snapshot.BufferItems.Add(new BeltBufferItemRecord(layout.Cells[layout.Cells.Length - 1].BlockInstanceId, held, i));
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
                    var cellIndex = cells.Length - 1 - states[order].DistanceToExit / BeltConstants.ItemWidth;
                    var distanceToCellExit = states[order].DistanceToExit % BeltConstants.ItemWidth;
                    snapshot.RunningItems.Add(new BeltRunningItemRecord(cells[cellIndex].BlockInstanceId, distanceToCellExit, states[order].Item, segmentIndex, order));
                }
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
