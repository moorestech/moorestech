using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Transport;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // tick境界で、形の一覧とCore segmentの一覧から全量を切り出す。サーバーの組からも、クライアントの複製からも同じ手順で切り出せる
    // Cuts the full state out of a shape list and the Core segments at a tick boundary; the same procedure serves the server assembly and the client replica
    public static class BeltTransportFullStateCapture
    {
        public static BeltTransportFullState Capture(BeltTransportAssembly assembly)
        {
            var shapes = new BeltSegmentShape[assembly.Layouts.Length];
            for (var i = 0; i < shapes.Length; i++) shapes[i] = BeltSegmentShape.FromLayout(assembly.Layouts[i]);
            return Capture(shapes, assembly.Segments);
        }

        public static BeltTransportFullState Capture(BeltSegmentShape[] shapes, BeltConveyorSegment[] segments)
        {
            var states = new BeltSegmentState[shapes.Length];
            for (var i = 0; i < states.Length; i++) states[i] = CaptureSegment(shapes[i], segments[i]);
            return new BeltTransportFullState(states);
        }

        private static BeltSegmentState CaptureSegment(BeltSegmentShape shape, BeltConveyorSegment segment)
        {
            // 走行中は出口に近い順。bufferは合流・分岐だけが持つ
            // Running items in exit order; only merges and branches hold a buffer
            var captured = segment.CaptureItems();
            var items = new BeltItemSnapshot[captured.Length];
            for (var i = 0; i < items.Length; i++) items[i] = new BeltItemSnapshot(captured[i].Item, captured[i].DistanceToExit);

            var hasBufferItem = false;
            var bufferItem = default(BeltItem);
            if (segment is BeltBufferedSegment buffered) hasBufferItem = buffered.Buffer.TryGetItem(out bufferItem);
            var bufferSnapshot = hasBufferItem ? new BeltItemSnapshot(bufferItem, 0) : default;
            return new BeltSegmentState(shape, segment.PriorityOrder, items, hasBufferItem, bufferSnapshot);
        }
    }
}
