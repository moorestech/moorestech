namespace Game.Train.RailGraph
{
    public sealed class RailSegment
    {
        public int StartNodeId { get; }
        public int EndNodeId { get; }
        public int Length => _length;

        private int _length;
        private int _edgeCount;

        public RailSegment(int startNodeId, int endNodeId, int length)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SetLength(length);
            _edgeCount = 0;
        }

        // 長さを更新する
        // Update the length value
        public void SetLength(int length)
        {
            _length = length;
        }

        // エッジ参照数を加算する
        // Increment the edge reference count
        public void AddEdgeReference()
        {
            _edgeCount++;
        }

        // エッジ参照数を減算し、削除可能か返す
        // Decrement the edge reference count and report removal readiness
        public bool RemoveEdgeReference()
        {
            _edgeCount--;
            return _edgeCount <= 0;
        }
    }
}
