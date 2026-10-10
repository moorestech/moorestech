using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 再構築の直前に、旧構成のアイテムをマス・buffer・内部segmentごとに取り出したもの。取り出しは旧segmentを変更しない
    // Items of the old assembly taken right before a rebuild, grouped by cell, buffer and internal segment; capturing leaves the old segments untouched
    public sealed class BeltTransportSnapshot
    {
        public readonly List<BeltRunningItemRecord> RunningItems;
        public readonly List<BeltBufferItemRecord> BufferItems;
        public readonly List<BeltInternalItemRecord> InternalItems;

        private BeltTransportSnapshot(List<BeltRunningItemRecord> runningItems, List<BeltBufferItemRecord> bufferItems, List<BeltInternalItemRecord> internalItems)
        {
            RunningItems = runningItems;
            BufferItems = bufferItems;
            InternalItems = internalItems;
        }

        public static BeltTransportSnapshot Capture(BeltTransportAssembly assembly)
        {
            var runningItems = new List<BeltRunningItemRecord>();
            var bufferItems = new List<BeltBufferItemRecord>();
            var internalItems = new List<BeltInternalItemRecord>();
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
                    bufferItems.Add(new BeltBufferItemRecord(layout.Cells[layout.Cells.Length - 1].BlockInstanceId, held, i));
            }
            return new BeltTransportSnapshot(runningItems, bufferItems, internalItems);

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
                    runningItems.Add(new BeltRunningItemRecord(cells[cellIndex].BlockInstanceId, distanceToCellExit, states[order].Item, segmentIndex, order));
                }
            }

            void CaptureInternal(BeltSegmentLayout layout, BeltConveyorSegment segment)
            {
                // 内部segmentは合流へ出す唯一の出力を持ち、その合流の先頭マスが所有block
                // An internal segment has its single output into a merge, whose head cell is the owning block
                var states = segment.CaptureItems();
                if (states.Length == 0) return;
                var merge = layouts[layout.Outputs[0].PartnerSegmentIndex];
                internalItems.Add(new BeltInternalItemRecord(merge.Cells[0].BlockInstanceId, layout.Inputs[0].Direction, states));
            }

            #endregion
        }
    }
}
