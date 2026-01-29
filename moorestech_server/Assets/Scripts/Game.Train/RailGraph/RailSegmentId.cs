using System;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     レールセグメントの正規化ID
    ///     Canonical identifier for a rail segment
    /// </summary>
    public readonly struct RailSegmentId : IEquatable<RailSegmentId>
    {
        private readonly int _fromNodeId;
        private readonly int _toNodeId;

        public RailSegmentId(int fromNodeId, int toNodeId)
        {
            _fromNodeId = fromNodeId;
            _toNodeId = toNodeId;
        }

        public int GetFromNodeId() => _fromNodeId;
        public int GetToNodeId() => _toNodeId;

        // ノードペアから正規化IDを生成する
        // Build a canonical id from a node pair
        public static RailSegmentId CreateCanonical(int fromNodeId, int toNodeId)
        {
            var alternateFrom = toNodeId ^ 1;
            var alternateTo = fromNodeId ^ 1;
            return fromNodeId <= alternateFrom ? new RailSegmentId(fromNodeId, toNodeId) : new RailSegmentId(alternateFrom, alternateTo);
        }

        public bool Equals(RailSegmentId other) => _fromNodeId == other._fromNodeId && _toNodeId == other._toNodeId;
        public override bool Equals(object obj) => obj is RailSegmentId other && Equals(other);

        // ノード順を反映してハッシュを計算する
        // Calculate hash from ordered nodes
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _fromNodeId;
                hashCode = (hashCode * 397) ^ _toNodeId;
                return hashCode;
            }
        }
    }
}
