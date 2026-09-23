using System;

namespace Game.BeltSegment
{
    /// <summary>合流・分岐segmentの終端にある1アイテムbuffer。</summary>
    public sealed class BeltBuffer : IBeltSource
    {
        readonly IBeltReceiver[] outputs = new IBeltReceiver[4];
        readonly BeltDirection[] outputDirections = new BeltDirection[3];
        BeltItem item;
        int outputCount, nextOutput;

        public BeltConveyorSegment Segment { get; }
        public bool HasItem { get; private set; }
        internal int PriorityIndex => nextOutput;

        internal BeltBuffer(BeltConveyorSegment segment, int priorityIndex)
        {
            Segment = segment;
            nextOutput = priorityIndex;
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
        }

        /// <summary>搬出先を登録する。登録順が初期優先順位。</summary>
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection)
        {
            target.AttachInput(this, BeltDirections.Opposite(outputDirection));
            outputs[(int)outputDirection] = target;
            outputDirections[outputCount++] = outputDirection;
        }

        /// <summary>段階2では、現在の最優先出力だけを搬出候補として提示する。</summary>
        public bool TryGetOutput(BeltDirection inputDirection)
        {
            return HasItem && outputDirections[nextOutput] == BeltDirections.Opposite(inputDirection);
        }

        /// <summary>段階1。出口でクランプしてから回収する。回収後に残りの移動量を使わない。</summary>
        internal void Collect()
        {
            if (!Segment.CollectForBuffer(out var collected)) return;
            item = collected;
            HasItem = true;
        }

        /// <summary>段階3。搬出成功時だけ、優先順位の開始位置を1つ進める。</summary>
        internal void Transfer()
        {
            if (!HasItem || Segment.TickSpeed == 0) return;
            // 搬出可能な候補を順に試し、成功した時だけ優先順位を進める。
            // Try outputs in order and rotate priority only after success.
            for (int offset = 0; offset < outputCount; offset++)
            {
                BeltDirection direction = outputDirections[(nextOutput + offset) % outputCount];
                var target = outputs[(int)direction];
                BeltDirection inputDirection = BeltDirections.Opposite(direction);
                int length = Math.Min(Segment.TickSpeed, target.GetOffer(inputDirection));
                if (length <= 0 || !target.TryReceive(inputDirection, length, item)) continue;
                item = default;
                HasItem = false;
                nextOutput = (nextOutput + 1) % outputCount;
                return;
            }
        }

    }
}
