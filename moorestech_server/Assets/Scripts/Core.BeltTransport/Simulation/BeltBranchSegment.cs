namespace Core.BeltTransport
{
    // 搬入元1つから終端bufferで最大3方向へ振り分ける分岐segment
    // A branch segment that distributes from one input to up to three directions through its end buffer
    public sealed class BeltBranchSegment : BeltBufferedSegment
    {
        public override BeltSegmentKind Kind => BeltSegmentKind.Branch;
        public override int PriorityOrder => Buffer.PriorityOrder;

        // priorityOrderは未接続方向も含む3方向の搬出順。InitializeFromDirectionなら直進の搬出を先頭に初期化する
        // forwardDirectionは終端マスの直進搬出方向
        // priorityOrder is the output order of three directions including unconnected ones; InitializeFromDirection starts with the straight output
        // forwardDirection is the straight output direction of the last cell
        public BeltBranchSegment(int capacity, int speed, int priorityOrder, BeltDirection forwardDirection)
            : base(capacity, speed, priorityOrder >= 0 ? priorityOrder : BeltPriority.Create(forwardDirection))
        {
        }
    }
}
