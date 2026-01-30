using Core.Master;

namespace Game.Train.RailGraph
{
    // 2つのノードを結ぶレールセグメント
    // Rail segment connecting two nodes with a rail item type
    public readonly struct RailSegment
    {
        private const byte MinToMaxFlag = 1;
        private const byte MaxToMinFlag = 2;
        private readonly RailSegmentId _segmentId;
        private readonly ItemId _railItemId;
        private readonly byte _directionFlags;

        public RailSegment(RailSegmentId segmentId, ItemId railItemId) : this(segmentId, railItemId, 0) { }

        public RailSegment(RailSegmentId segmentId, ItemId railItemId, byte directionFlags)
        {
            _segmentId = segmentId;
            _railItemId = railItemId;
            _directionFlags = directionFlags;
        }

        public RailSegmentId GetSegmentId() => _segmentId;
        public ItemId GetRailItemId() => _railItemId;
        public byte GetDirectionFlags() => _directionFlags;

        // min->max方向の接続を持つかどうかを返す
        // Check whether the segment has a min->max connection
        public bool HasMinToMax() => (_directionFlags & MinToMaxFlag) != 0;

        // max->min方向の接続を持つかどうかを返す
        // Check whether the segment has a max->min connection
        public bool HasMaxToMin() => (_directionFlags & MaxToMinFlag) != 0;

        // いずれかの方向の接続を持つかどうかを返す
        // Check whether the segment has any connection direction
        public bool HasAnyDirection() => _directionFlags != 0;

        public RailSegment WithRailItemId(ItemId railItemId) => new RailSegment(_segmentId, railItemId, _directionFlags);

        public RailSegment AddDirection(bool isMinToMax)
        {
            var flag = GetDirectionFlag(isMinToMax);
            return new RailSegment(_segmentId, _railItemId, (byte)(_directionFlags | flag));
        }

        public RailSegment RemoveDirection(bool isMinToMax)
        {
            var flag = GetDirectionFlag(isMinToMax);
            return new RailSegment(_segmentId, _railItemId, (byte)(_directionFlags & ~flag));
        }

        public static byte GetDirectionFlag(bool isMinToMax) => isMinToMax ? MinToMaxFlag : MaxToMinFlag;
    }
}
