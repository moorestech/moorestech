using System;

namespace Game.Train.RailGraph
{
    public sealed class RailSegment
    {
        public int StartNodeId { get; }
        public int EndNodeId { get; }
        public int Length => _length;
        public Guid RailTypeGuid => _railTypeGuid;
        public bool IsDrawable => _isDrawable;

        private int _length;
        private Guid _railTypeGuid;
        private bool _isDrawable;

        public RailSegment(int startNodeId, int endNodeId, int length, Guid railTypeGuid)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SetLength(length);
            SetRailType(railTypeGuid);
            SetDrawable(true);
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

        // 描画可否を更新する
        // Update the drawable flag
        public void SetDrawable(bool isDrawable)
        {
            _isDrawable = isDrawable;
        }
    }
}
