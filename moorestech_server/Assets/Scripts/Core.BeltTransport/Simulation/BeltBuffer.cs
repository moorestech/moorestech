// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation; see LICENSE.txt.
using System;

namespace Core.BeltTransport
{
    /// <summary>合流・分岐segmentの終端にある1アイテムbuffer。</summary>
    public sealed class BeltBuffer : IBeltSource
    {
        readonly IBeltReceiver[] outputs = new IBeltReceiver[4];
        readonly int priorityCount;
        BeltItem item;
        int outputMask;

        private BeltConveyorSegment Segment { get; }
        public bool HasItem { get; private set; }
        internal int PriorityOrder { get; private set; }
        private readonly IBeltItemMovementObserver movement;

        internal BeltBuffer(BeltConveyorSegment segment, int priorityOrder, IBeltItemMovementObserver movement)
        {
            this.movement = movement;
            Segment = segment;
            priorityCount = segment.Kind == BeltSegmentKind.Branch ? 3 : 1;
            PriorityOrder = priorityOrder;
        }

        /// <summary>tick境界で保持中のアイテムを読み取る。</summary>
        public bool TryGetItem(out BeltItem item)
        {
            item = this.item;
            return HasItem;
        }

        /// <summary>再生成した空のbufferへ、存続するblockのアイテムを復元する。</summary>
        public void RestoreItem(in BeltItem item)
        {
            this.item = item;
            HasItem = true;
            movement.Insert(0);
        }

        /// <summary>搬出先と接続方向を登録する。3方向の優先順序は維持する。</summary>
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection)
        {
            // 未接続方向も含めた優先順序を保つ。
            // Preserve the order including disconnected directions.
            target.AttachInput(this, BeltDirections.Opposite(outputDirection));
            outputs[(int)outputDirection] = target;
            outputMask |= 1 << (int)outputDirection;
            if (priorityCount == 1) PriorityOrder = (int)outputDirection;
        }

        /// <summary>段階2では、接続済み方向のうち最優先の出力だけを提示する。</summary>
        public bool TryGetOutput(BeltDirection inputDirection)
        {
            // 停止中のbufferは合流予約を占有しない。
            // A stopped buffer must not reserve a merge input.
            return HasItem && 0 < Segment.TickSpeed && BeltPriority.FirstConnected(PriorityOrder, outputMask)
                == (int)BeltDirections.Opposite(inputDirection);
        }

        /// <summary>段階1。出口でクランプしてから回収する。回収後に残りの移動量を使わない。</summary>
        internal void Collect()
        {
            if (!Segment.CollectForBuffer(out var collected)) return;
            item = collected;
            HasItem = true;
            movement.Insert(0);
        }

        /// <summary>段階3。搬出成功方向を優先順序の末尾へ移す。</summary>
        internal void Transfer()
        {
            if (!HasItem || Segment.TickSpeed == 0) return;
            for (int offset = 0; offset < priorityCount; offset++)
            {
                BeltDirection direction = (BeltDirection)BeltPriority.Direction(PriorityOrder, offset);
                if ((outputMask & (1 << (int)direction)) == 0) continue;
                var target = outputs[(int)direction];
                BeltDirection inputDirection = BeltDirections.Opposite(direction);
                int length = Math.Min(Segment.TickSpeed, target.GetOffer(inputDirection));
                if (length <= 0 || !target.TryReceive(inputDirection, length, item)) continue;
                // 成功した方向だけを末尾へ送る。
                // Move only the successful direction to the end.
                movement.Remove(0);
                item = default;
                HasItem = false;
                if (priorityCount == 3) PriorityOrder = BeltPriority.MoveLast(PriorityOrder, (int)direction);
                return;
            }
        }

    }
}
