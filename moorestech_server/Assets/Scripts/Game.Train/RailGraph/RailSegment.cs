using Core.Master;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     ???2????????????
    ///     Rail segment connecting two nodes with a rail item type
    /// </summary>
    public readonly struct RailSegment
    {
        private readonly RailSegmentId _segmentId;
        private readonly ItemId _railItemId;

        public RailSegment(RailSegmentId segmentId, ItemId railItemId)
        {
            _segmentId = segmentId;
            _railItemId = railItemId;
        }

        public RailSegmentId GetSegmentId() => _segmentId;
        public ItemId GetRailItemId() => _railItemId;
    }
}
