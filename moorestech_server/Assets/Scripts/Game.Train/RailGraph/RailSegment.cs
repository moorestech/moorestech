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
        private Guid _railTypeGuid;

        public RailSegment(int startNodeId, int endNodeId, int length, Guid railTypeGuid)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SetLength(length);
            SetRailType(railTypeGuid);
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
    }
}
