using UnityEngine;

namespace Game.Train.RailGraph
{
    public sealed class RailSegment
    {
        public int StartNodeId { get; }
        public int EndNodeId { get; }
        public int Length => _length;
        public float BezierStrength => _bezierStrength;

        private int _length;
        private float _bezierStrength;
        private int _edgeCount;

        public RailSegment(int startNodeId, int endNodeId, int length, float bezierStrength)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SetLength(length);
            SetBezierStrength(bezierStrength);
            _edgeCount = 0;
        }

        // 長さを更新する
        // Update the length value
        public void SetLength(int length)
        {
            _length = length;
        }

        // ベジエ強度を更新する
        // Update the bezier strength value
        public void SetBezierStrength(float strength)
        {
            _bezierStrength = strength;
        }

        // 参照辺数を加算する
        // Increment the edge reference count
        public void AddEdgeReference()
        {
            _edgeCount++;
        }

        // 参照辺数を減算し、0以下なら削除対象とする
        // Decrement the edge reference count and report removal readiness
        public bool RemoveEdgeReference()
        {
            _edgeCount--;
            return _edgeCount <= 0;
        }
    }
}
