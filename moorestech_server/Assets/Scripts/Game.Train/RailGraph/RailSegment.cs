using System;

namespace Game.Train.RailGraph
{
    public sealed class RailSegment
    {
        public int StartNodeId { get; }
        public int EndNodeId { get; }
        public int Length => _length;
        public Guid RailTypeGuid => _railTypeGuid;

        private int _length;
        private int _edgeCount;
        private Guid _railTypeGuid;

        public RailSegment(int startNodeId, int endNodeId, int length, Guid railTypeGuid)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SetLength(length);
            SetRailType(railTypeGuid);
            _edgeCount = 0;
        }

        // Update the length value
        public void SetLength(int length)
        {
            _length = length;
        }

        // Update the rail type value
        public void SetRailType(Guid railTypeGuid)
        {
            _railTypeGuid = railTypeGuid;
        }

        // Increment the edge reference count
        public void AddEdgeReference()
        {
            _edgeCount++;
        }

        // Decrement the edge reference count and report removal readiness
        public bool RemoveEdgeReference()
        {
            _edgeCount--;
            return _edgeCount <= 0;
        }
    }
}
