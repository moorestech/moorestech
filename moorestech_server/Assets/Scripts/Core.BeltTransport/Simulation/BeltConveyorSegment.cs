// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation; see LICENSE.txt.
using System;

namespace Core.BeltTransport
{
    public sealed class BeltConveyorSegment : IBeltSource, IBeltReceiver
    {
        private readonly BeltItemQueue queue;
        private readonly BeltSegmentInputs inputs;
        private BeltDirection outputDirection;
        private BeltSegmentTransfer deferredOutput;
        private int Length => Capacity * BeltConstants.ItemWidth;
        private int OutputLength => Count == 0 ? 0 : TickSpeed - queue.HeadGap;
        public BeltSegmentKind Kind { get; }
        public BeltBuffer Buffer { get; }
        private IBeltReceiver Output;
        private int Capacity { get; }
        private int Count => queue.Count;
        private int Speed;
        public int PriorityOrder => Kind == BeltSegmentKind.Merge ? inputs.PriorityOrder
            : Kind == BeltSegmentKind.Branch ? Buffer.PriorityOrder : 0;
        internal int TickSpeed { get; private set; }
        internal BeltDirection InputDirection { get; private set; } = BeltDirection.None;

        public BeltConveyorSegment(int capacity, int speed, BeltSegmentKind kind,
            int priorityOrder, BeltDirection forwardDirection)
            : this(capacity, speed, kind, priorityOrder, forwardDirection, UntrackedBeltMovement.Instance) { }

        internal BeltConveyorSegment(int capacity, int speed, BeltSegmentKind kind,
            int priorityOrder, BeltDirection forwardDirection, IBeltItemMovementObserver movement)
        {
            // 占有長が整数範囲を超えない容量に制限する。
            // Bound capacity so occupied length fits in an integer.
            if (capacity <= 0 || (int.MaxValue - (BeltConstants.ItemWidth - 1)) / BeltConstants.ItemWidth < capacity ||
                (kind == BeltSegmentKind.Merge && capacity != 1))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (kind != BeltSegmentKind.Normal && kind != BeltSegmentKind.Merge && kind != BeltSegmentKind.Branch)
                throw new ArgumentOutOfRangeException(nameof(kind));
            Capacity = capacity;
            Kind = kind;
            SetSpeed(speed);
            queue = new BeltItemQueue(capacity, movement);

            // 新規生成は向きから初期化し、ロードは保存順を使う。
            // Initialize from orientation or restore the saved priority order.
            inputs = new BeltSegmentInputs(kind == BeltSegmentKind.Merge
                ? (0 <= priorityOrder ? priorityOrder : BeltPriority.Create(BeltDirections.Opposite(forwardDirection))) : 0);
            if (kind != BeltSegmentKind.Normal)
                Buffer = new BeltBuffer(this, kind == BeltSegmentKind.Branch
                    ? (0 <= priorityOrder ? priorityOrder : BeltPriority.Create(forwardDirection))
                    : (int)forwardDirection, movement);

            #region Internal
            void SetSpeed(int speed)
            {
                if (speed < 0 || BeltConstants.ItemWidth / 2 < speed)
                    throw new ArgumentOutOfRangeException(nameof(speed));
                Speed = speed;
            }
            #endregion
        }

        public void ConnectTo(IBeltReceiver target, BeltDirection direction)
        {
            // 両端に同じ接続を反対方向として登録する。
            // Register both ends using opposite directions.
            target.AttachInput(this, BeltDirections.Opposite(direction));
            outputDirection = direction;
            Output = target;
        }

        public void AttachInput(IBeltSource source, BeltDirection direction) => inputs.Attach(source, direction);

        public int GetOffer(BeltDirection direction)
        {
            if (Kind == BeltSegmentKind.Merge && direction != inputs.ReservedDirection) return 0;
            return Length - queue.TotalLength;
        }

        public bool TryReceive(BeltDirection direction, int length, in BeltItem item)
        {
            // 搬入拒否は通常の流量制御。成功時だけ順序を更新する。
            // Rejection is normal flow control; rotate priority only on success.
            int offer = GetOffer(direction);
            if (offer < length) return false;
            queue.EnqueueTail(offer - length, item);
            if (Kind == BeltSegmentKind.Merge) { inputs.CompleteInput(direction); InputDirection = direction; }
            return true;
        }

        public bool TryGetOutput(BeltDirection direction) => 0 < OutputLength;
        internal void BeginTick() => TickSpeed = Speed;
        internal void RestoreInputDirection(BeltDirection direction) => InputDirection = direction;
        internal void ResolveInput() => inputs.Resolve(Count != 0);

        internal BeltSegmentTransfer CacheTransfer()
        {
            // 通常segment間だけ搬送を遅延確定する。
            // Defer transfers only between normal segments.
            var target = Output as BeltConveyorSegment;
            deferredOutput = target != null && target.Kind == BeltSegmentKind.Normal
                ? new BeltSegmentTransfer(target, BeltDirections.Opposite(outputDirection)) : null;
            return deferredOutput;
        }

        internal bool CollectForBuffer(out BeltItem item)
        {
            // 回収後には残りの移動量を消費しない。
            // Do not consume remaining movement after collection.
            queue.Advance(false, TickSpeed);
            item = default;
            if (Buffer.HasItem || Count == 0 || queue.HeadGap != 0) return false;
            item = queue.HeadItem;
            queue.DequeueHead();
            return true;
        }

        internal void AdvanceAndTransfer()
        {
            // 搬出成否を後続の前進にも反映する。
            // Output success also determines follower movement.
            bool sent = false;
            int length = OutputLength;
            if (0 < length && Output != null)
                sent = deferredOutput != null
                    ? deferredOutput.TryReceive(length, queue.HeadItem)
                    : Output.TryReceive(BeltDirections.Opposite(outputDirection), length, queue.HeadItem);
            queue.Advance(sent, TickSpeed);
        }

        internal void ReceiveTransferred(int length, in BeltItem item)
        {
            queue.EnqueueTail(Length - length - queue.TotalLength, item);
        }

        // tick境界で出口に近い順の位置を複製する。
        // Capture positions in exit-first order at tick boundaries.
        public BeltItemState[] CaptureItems() => queue.CaptureItems();

        // 呼び出し側が距離と間隔を確定した列を空のsegmentへ復元する。
        // Restore caller-validated distances and spacing into an empty segment.
        public void RestoreItems(BeltItemState[] states)
        {
            foreach (var state in states)
                queue.EnqueueTail(state.DistanceToExit - queue.TotalLength, state.Item);
        }
    }
}
