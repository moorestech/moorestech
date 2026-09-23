using System;

namespace Game.BeltSegment
{
    public struct BeltItem
    {
        public Guid Guid;
        public int ItemId;
        public BeltDirection AcceptedInput;
        public ItemPosition Position;
    }
}
