using System;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 配置の優先度。値が小さいほど先に置く
    // Placement priority; smaller values are placed first
    public enum BeltRestorePriority
    {
        RunningMatchingPath = 1,
        RunningRealigned = 2,
        VanishedBufferItem = 3,
    }

    // 新しいsegmentへ置こうとするアイテム1個。優先度→出口に近い順→元の並びの順で配置を確定する
    // One item to place into a new segment; placement is settled by priority, then nearness to the exit, then the original order
    public readonly struct BeltRestoreCandidate : IComparable<BeltRestoreCandidate>
    {
        public readonly BeltRestorePriority Priority;
        public readonly int SegmentIndex;
        public readonly int DistanceToExit;
        public readonly BeltItem Item;
        private readonly int _sourceSegmentIndex;
        private readonly int _sourceOrder;

        public BeltRestoreCandidate(BeltRestorePriority priority, int segmentIndex, int distanceToExit, in BeltItem item, int sourceSegmentIndex, int sourceOrder)
        {
            Priority = priority;
            SegmentIndex = segmentIndex;
            DistanceToExit = distanceToExit;
            Item = item;
            _sourceSegmentIndex = sourceSegmentIndex;
            _sourceOrder = sourceOrder;
        }

        public int CompareTo(BeltRestoreCandidate other)
        {
            if (Priority != other.Priority) return ((int)Priority).CompareTo((int)other.Priority);
            if (DistanceToExit != other.DistanceToExit) return DistanceToExit.CompareTo(other.DistanceToExit);
            if (_sourceSegmentIndex != other._sourceSegmentIndex) return _sourceSegmentIndex.CompareTo(other._sourceSegmentIndex);
            return _sourceOrder.CompareTo(other._sourceOrder);
        }
    }
}
