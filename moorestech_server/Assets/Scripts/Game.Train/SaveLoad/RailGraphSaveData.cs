using System;

namespace Game.Train.SaveLoad
{
    [Serializable]
    public class RailSegmentSaveData
    {
        // ??????????????
        // Save data for a rail segment
        public ConnectionDestination A;
        public ConnectionDestination B;
        public int Length;
        public Guid RailTypeGuid;
        public bool IsDrawable;
    }
}
