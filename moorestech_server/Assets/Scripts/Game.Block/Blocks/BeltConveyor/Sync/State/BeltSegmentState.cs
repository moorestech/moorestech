namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量に載せるsegment1本の形と、そのtick境界での中身(優先順・走行中アイテム・buffer内アイテム)
    // One segment in the full state: its shape plus its contents at the tick boundary (priority order, running items, buffered item)
    public sealed class BeltSegmentState
    {
        public readonly BeltSegmentShape Shape;
        // 合流は搬入方向、分岐はbufferの搬出方向の3方向の並び。通常は0
        // Three-direction order: input order for a merge, buffer output order for a branch, 0 for a normal segment
        public readonly int PriorityOrder;
        // 現在の速度。歯車ベルコンは回転数で変わるので形(マスタ速度)とは別に持つ
        // The current speed, kept apart from the shape's master speed because gear belts follow the rotation
        public readonly int CurrentSpeed;
        // 出口に近い順
        // In exit order
        public readonly BeltItemSnapshot[] Items;
        public readonly bool HasBufferItem;
        public readonly BeltItemSnapshot BufferItem;

        public BeltSegmentState(BeltSegmentShape shape, int priorityOrder, int currentSpeed, BeltItemSnapshot[] items, bool hasBufferItem, BeltItemSnapshot bufferItem)
        {
            Shape = shape;
            PriorityOrder = priorityOrder;
            CurrentSpeed = currentSpeed;
            Items = items;
            HasBufferItem = hasBufferItem;
            BufferItem = bufferItem;
        }
    }
}
