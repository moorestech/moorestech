namespace Core.BeltTransport
{
    // 終端に1アイテムbufferを持ち、段階1で前進してbufferへ回収する合流・分岐segmentの共通部
    // The shared part of merge and branch segments: a one-item buffer at the end, advancing and collecting into it in stage 1
    public abstract class BeltBufferedSegment : BeltConveyorSegment
    {
        public BeltBuffer Buffer { get; }

        // bufferPriorityOrderは分岐の3方向の搬出順、または合流の唯一の搬出方向
        // bufferPriorityOrder is a branch's three-direction output order, or a merge's only output direction
        internal BeltBufferedSegment(int capacity, int speed, int bufferPriorityOrder) : base(capacity, speed)
        {
            Buffer = new BeltBuffer(this, bufferPriorityOrder);
        }

        // 段階1。bufferが空で先頭がこのtickで出口に届くなら、先頭を回収して後続は満額進める(通常segmentの搬出成立と同じ扱い)
        // Stage 1. When the buffer is empty and the head reaches the exit this tick, collect it and let the followers advance the full speed (as a normal segment's successful output)
        // 先頭の超過分は回収に使うので捨ててよい。クランプして後続まで止めると、密着した列が超過分だけ進行量を失いスループットが100%を割る(ADR 0084 F1)
        // The head's overshoot is spent on the collection and can be dropped; clamping the followers too would cost a packed run that overshoot and break 100% throughput (ADR 0084 F1)
        // bufferが満杯のときだけ出口でクランプする
        // Only a full buffer clamps the run at the exit
        internal bool CollectForBuffer(out BeltItem item)
        {
            item = default;
            if (Buffer.HasItem || Count == 0 || OutputLength < 0)
            {
                Advance(false);
                return false;
            }
            item = HeadItem;
            Advance(true);
            return true;
        }
    }
}
