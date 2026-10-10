using System;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 新しいsegmentへ置こうとするアイテム1個。優先度→出口に近い順→元の並びの順で配置を確定する
    // One item to place into a new segment; placement is settled by priority, then nearness to the exit, then the original order
    public readonly struct BeltRestoreCandidate : IComparable<BeltRestoreCandidate>
    {
        // 1: 進入方向が新しい経路と一致する走行中、2: 一致しない走行中、3: 消えるbufferのアイテム
        // 1: running with a matching entry direction, 2: running with a mismatch, 3: item of a vanishing buffer
        public readonly int Priority;
        public readonly int SegmentIndex;
        public readonly int DistanceToExit;
        public readonly BeltItem Item;
        private readonly int _sourceSegmentIndex;
        private readonly int _sourceOrder;

        public BeltRestoreCandidate(int priority, int segmentIndex, int distanceToExit, in BeltItem item, int sourceSegmentIndex, int sourceOrder)
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
            if (Priority != other.Priority) return Priority.CompareTo(other.Priority);
            if (DistanceToExit != other.DistanceToExit) return DistanceToExit.CompareTo(other.DistanceToExit);
            if (_sourceSegmentIndex != other._sourceSegmentIndex) return _sourceSegmentIndex.CompareTo(other._sourceSegmentIndex);
            return _sourceOrder.CompareTo(other._sourceOrder);
        }
    }
}
