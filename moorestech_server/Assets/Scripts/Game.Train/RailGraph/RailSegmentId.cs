using System;

namespace Game.Train.RailGraph
{
    // レールセグメントの正規化ID
    // Canonical identifier for a rail segment
    public readonly struct RailSegmentId : IEquatable<RailSegmentId>
    {
        private readonly int _minNodeId;
        private readonly int _maxNodeId;

        public RailSegmentId(int minNodeId, int maxNodeId)
        {
            _minNodeId = minNodeId;
            _maxNodeId = maxNodeId;
        }

        public int GetMinNodeId() => _minNodeId;
        public int GetMaxNodeId() => _maxNodeId;

        // ノードIDの組み合わせから正規化IDを作成する
        // Build a canonical id from a node pair
        public static RailSegmentId CreateCanonical(int fromNodeId, int toNodeId)
        {
            var alternateFrom = toNodeId ^ 1;
            var alternateTo = fromNodeId ^ 1;
            return fromNodeId <= alternateFrom ? new RailSegmentId(fromNodeId, toNodeId) : new RailSegmentId(alternateFrom, alternateTo);
        }

        // 入力方向がmin->maxかどうかを判定する
        // Check whether the input direction is min->max
        public static bool IsMinToMaxDirection(int fromNodeId, int toNodeId)
        {
            var canonical = CreateCanonical(fromNodeId, toNodeId);
            return canonical._minNodeId == fromNodeId && canonical._maxNodeId == toNodeId;
        }

        public bool Equals(RailSegmentId other) => _minNodeId == other._minNodeId && _maxNodeId == other._maxNodeId;
        public override bool Equals(object obj) => obj is RailSegmentId other && Equals(other);

        // 正規化ノードからハッシュを計算する
        // Calculate hash from ordered nodes
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _minNodeId;
                hashCode = (hashCode * 397) ^ _maxNodeId;
                return hashCode;
            }
        }
    }
}
