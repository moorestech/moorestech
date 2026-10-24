using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // tick境界で、サーバーの組の形の一覧とCore segmentの一覧から全量(送受信する形)を切り出す。途中参加と再構築tickの全量送信だけが使い、ハッシュは切り出さずに直接混ぜる
    // Cuts the full state (the wire form) out of the server assembly's shapes and Core segments at a tick boundary; only the join-time and rebuilt-tick full-state sends use it, while the hash mixes directly without a capture
    public static class BeltTransportFullStateCapture
    {
        public static BeltTransportFullState Capture(BeltSegmentShape[] shapes, BeltConveyorSegment[] segments)
        {
            var states = new BeltSegmentState[shapes.Length];
            for (var i = 0; i < states.Length; i++) states[i] = CaptureSegment(shapes[i], segments[i]);
            return new BeltTransportFullState(states);

            #region Internal

            BeltSegmentState CaptureSegment(BeltSegmentShape shape, BeltConveyorSegment segment)
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
                return new BeltSegmentState(shape, segment.PriorityOrder, segment.Speed, items, hasBufferItem, bufferSnapshot);
            }

            #endregion
        }
    }
}
