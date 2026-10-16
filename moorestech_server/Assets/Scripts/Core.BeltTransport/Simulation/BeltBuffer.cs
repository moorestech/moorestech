using System;

namespace Core.BeltTransport
{
    // 合流・分岐segmentの終端にある1アイテムbuffer
    // A one-item buffer at the end of a merge or branch segment
    public sealed class BeltBuffer : IBeltSource
    {
        private readonly IBeltReceiver[] _outputs = new IBeltReceiver[4];
        private readonly BeltEntryDirection[] _outputEntryDirections = new BeltEntryDirection[4];
        private readonly int _priorityCount;
        private BeltItem _item;
        private int _outputMask;
        private int _outputOrder;

        public BeltBufferedSegment Segment { get; }
        public bool HasItem { get; private set; }
        internal int PriorityOrder => _outputOrder;

        internal BeltBuffer(BeltBufferedSegment segment, int priorityOrder)
        {
            // 分岐は3方向を順に試し、合流は唯一の搬出方向だけを持つ
            // A branch tries three directions in order; a merge has its single output direction only
            Segment = segment;
            _priorityCount = segment.Kind == BeltSegmentKind.Branch ? 3 : 1;
            _outputOrder = priorityOrder;
        }

        // tick境界で保持中のアイテムを読み取る
        // Read the held item at a tick boundary
        public bool TryGetItem(out BeltItem item)
        {
            item = _item;
            return HasItem;
        }

        // 再生成した空のbufferへ、存続するblockのアイテムを復元する
        // Restore a surviving block's item into a rebuilt empty buffer
        public void RestoreItem(in BeltItem item)
        {
            _item = item;
            HasItem = true;
        }

        // 搬出先と接続方向を登録する。3方向の優先順は維持し、合流は唯一の方向を順序にする
        // Register an output and its direction. Keeps the three-direction order; a merge uses its only direction as the order
        // entryDirectionは受け入れ側マスから見た12通りの進入方向で、この方向へ渡すアイテムへ書く
        // entryDirection is the 12-way entry direction seen from the receiving cell, written into items handed this way
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection, BeltEntryDirection entryDirection)
        {
            target.AttachInput(this, BeltDirections.Opposite(outputDirection));
            _outputs[(int)outputDirection] = target;
            _outputEntryDirections[(int)outputDirection] = entryDirection;
            _outputMask |= 1 << (int)outputDirection;
            if (_priorityCount == 1) _outputOrder = (int)outputDirection;
        }

        // 段階2では、接続済み方向のうち最優先の出力だけを提示する。そのtickの速度が0なら搬出不可と答える
        // In stage 2, offer only the highest-priority connected output. Answers no when this tick's speed is 0
        public bool TryGetOutput(BeltDirection inputDirection)
        {
            return HasItem && Segment.TickSpeed != 0 && BeltPriority.FirstConnected(_outputOrder, _outputMask)
                == (int)BeltDirections.Opposite(inputDirection);
        }

        // 段階1。出口でクランプしてから回収する。回収後に残りの移動量を使わない
        // Stage 1. Clamp at the exit, then collect. The remaining advance is not used after collecting
        internal void Collect()
        {
            if (!Segment.CollectForBuffer(out var collected)) return;
            _item = collected;
            HasItem = true;
        }

        // 段階3。接続済み方向を優先順に試し、搬出成功方向を優先順の末尾へ移す
        // Stage 3. Try connected directions in priority order and move the succeeded one to the end
        internal void Transfer()
        {
            if (!HasItem || Segment.TickSpeed == 0) return;
            for (var offset = 0; offset < _priorityCount; offset++)
            {
                var direction = (BeltDirection)BeltPriority.Direction(_outputOrder, offset);
                if ((_outputMask & (1 << (int)direction)) == 0) continue;
                var target = _outputs[(int)direction];

                // 進入距離は送り元segmentの速度と送り先の空きの小さい方
                // The entry length is the smaller of the source segment's speed and the target's offer
                var inputDirection = BeltDirections.Opposite(direction);
                var length = Math.Min(Segment.TickSpeed, target.GetOffer(inputDirection));
                if (length <= 0) continue;

                // 搬出先のマスへ入るので、渡す複製へこの方向の進入方向を書く
                // The item enters the target's cell, so the handed copy carries this direction's entry direction
                var item = _item.WithEntryDirection(_outputEntryDirections[(int)direction]);
                if (!target.TryReceive(inputDirection, length, item)) continue;
                _item = default;
                HasItem = false;
                if (_priorityCount == 3) _outputOrder = BeltPriority.MoveLast(_outputOrder, (int)direction);
                return;
            }
        }
    }
}
