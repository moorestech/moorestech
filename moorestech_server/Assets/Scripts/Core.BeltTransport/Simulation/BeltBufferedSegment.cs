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

        // 段階1。前進して出口でクランプし、bufferが空で先頭が出口ちょうどなら取り出す
        // Stage 1. Advance clamped at the exit; take the head when the buffer is empty and the head is exactly at the exit
        internal bool CollectForBuffer(out BeltItem item)
        {
            Advance(false);
            item = default;
            if (Buffer.HasItem) return false;
            return TryDequeueHeadAtExit(out item);
        }
    }
}
