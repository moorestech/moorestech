using System;

namespace Game.Train.SaveLoad
{
    [Serializable]
    public class RailSegmentSaveData
    {
        // レールセグメントの保存データ
        // Save data for a rail segment
        public ConnectionDestination A;
        public ConnectionDestination B;
        public int Length;
    }
}
