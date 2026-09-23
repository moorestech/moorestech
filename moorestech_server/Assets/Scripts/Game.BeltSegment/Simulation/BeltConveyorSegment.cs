using System;

namespace Game.BeltSegment
{
    /// <summary>ポート、合流予約、速度、段階ごとの操作を管理するsegment。</summary>
    /// <remarks>
    /// speedは0～ItemWidth/2、搬入lengthは1～ItemWidth。外部供給と配線変更はtick境界で行う。
    /// 通常segmentは段階4、合流・分岐segmentはbufferにより段階1で更新する。
    /// 走行列の隙間と密着ブロックはBeltItemQueueが管理する。
    /// </remarks>
    public sealed class BeltConveyorSegment : IBeltSource, IBeltReceiver
    {
        readonly int n;
        int tickSpeed;
        readonly BeltItemQueue queue;
        readonly IBeltSource[] inputs = new IBeltSource[3];
        readonly BeltDirection[] inputDirections = new BeltDirection[3];
        BeltDirection reservedInputDirection = BeltDirection.None;
        BeltDirection outputDirection;
        int inputCount, nextInput;
        int Length => n * BeltConstants.ItemWidth;

        public BeltSegmentKind Kind { get; }
        public BeltBuffer Buffer { get; }
        public IBeltReceiver Output { get; private set; }
        public int Capacity => n;
        public int Count => queue.Count;
        /// <summary>blockへ保存するラウンドロビンの開始index。通常segmentは0。</summary>
        public int PriorityIndex => Kind == BeltSegmentKind.Merge ? nextInput
            : Kind == BeltSegmentKind.Branch ? Buffer.PriorityIndex : 0;
        internal int TickSpeed => tickSpeed;

        /// <summary>段階0より前に変更する。tick中は固定。0では走行しない。</summary>
        public int Speed { get; private set; }

        public void SetSpeed(int speed)
        {
            if (speed > BeltConstants.ItemWidth / 2)
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        /// <summary>先頭から最後尾アイテムの後端までの占有長。O(1)。</summary>
        int TotalLength => queue.TotalLength;
        /// <summary>このtickで先頭が出口を越える距離。正数なら搬出可能。空なら0。</summary>
        int OutputLength => queue.Count == 0 ? 0 : tickSpeed - queue.HeadGap;

        /// <param name="priorityIndex">存続するblockから引き継ぐ開始index。新しいblockは0。</param>
        public BeltConveyorSegment(int capacity, int speed, BeltSegmentKind kind, int priorityIndex)
        {
            if (capacity <= 0 || capacity > (int.MaxValue - (BeltConstants.ItemWidth - 1)) / BeltConstants.ItemWidth ||
                (kind == BeltSegmentKind.Merge && capacity != 1))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (speed > BeltConstants.ItemWidth / 2) throw new ArgumentOutOfRangeException(nameof(speed));
            if (kind != BeltSegmentKind.Normal && kind != BeltSegmentKind.Merge && kind != BeltSegmentKind.Branch)
                throw new ArgumentOutOfRangeException(nameof(kind));
            n = capacity;
            SetSpeed(speed);
            Kind = kind;
            if (kind == BeltSegmentKind.Merge) nextInput = priorityIndex;
            if (kind != BeltSegmentKind.Normal)
                Buffer = new BeltBuffer(this, kind == BeltSegmentKind.Branch ? priorityIndex : 0);
            queue = new BeltItemQueue(n);
        }

        /// <summary>搬出先を登録し、相手の搬入元へこのsegmentを追加する。</summary>
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection)
        {
            target.AttachInput(this, BeltDirections.Opposite(outputDirection));
            this.outputDirection = outputDirection;
            Output = target;
        }

        /// <summary>登録順が初期搬入優先順位。機械の搬出元もこのインターフェースで登録できる。</summary>
        public void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
            inputs[inputCount] = source;
            inputDirections[inputCount++] = inputDirection;
        }

        public int GetOffer(BeltDirection inputDirection)
        {
            // 合流は段階2で予約した方向からだけ受け入れる。
            // A merge accepts only the direction reserved in phase 2.
            if (Kind == BeltSegmentKind.Merge && inputDirection != reservedInputDirection)
                return 0;
            return Length - TotalLength;
        }

        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            int offer = GetOffer(inputDirection);
            if (length > offer) return false;
            queue.EnqueueTail(offer - length, item);
            if (Kind == BeltSegmentKind.Merge)
            {
                nextInput = (nextInput + 1) % inputCount;
            }
            return true;
        }

        public bool TryGetOutput(BeltDirection inputDirection) => OutputLength > 0;

        internal void BeginTick()
        {
            tickSpeed = Speed;
        }

        /// <summary>段階2。空の合流segmentについて搬入元1つまたは搬入なしを固定する。</summary>
        internal void ResolveInput()
        {
            reservedInputDirection = BeltDirection.None;
            if (queue.Count != 0) return;
            for (int offset = 0; offset < inputCount; offset++)
            {
                int index = (nextInput + offset) % inputCount;
                BeltDirection direction = inputDirections[index];
                if (!inputs[index].TryGetOutput(direction)) continue;
                reservedInputDirection = direction;
                return;
            }
        }

        internal bool CollectForBuffer(out BeltItem item)
        {
            // 段階1で前進と回収を一度だけ行う。
            // Advance and collect once in phase 1.
            queue.Advance(tickSpeed, false);
            item = default;
            if (Buffer.HasItem || queue.Count == 0 || queue.HeadGap != 0) return false;
            item = queue.HeadItem;
            queue.DequeueHead();
            return true;
        }

        /// <summary>段階4。段階3に受け取ったアイテムも含めて前進する。</summary>
        internal void AdvanceAndTransfer()
        {
            bool sent = false;
            int length = OutputLength;
            if (length > 0 && Output != null)
                sent = Output.TryReceive(BeltDirections.Opposite(outputDirection), length, queue.HeadItem);
            queue.Advance(tickSpeed, sent);
        }

        /// <summary>tick境界で、出口に近い順のアイテムと出口までの距離を複製する。</summary>
        public BeltItemState[] CaptureItems() => queue.CaptureItems();

        /// <summary>再生成した空のsegmentへ、出口に近い順のアイテムを復元する。</summary>
        public void RestoreItems(BeltItemState[] restoredItems) => queue.RestoreItems(restoredItems);
    }
}
