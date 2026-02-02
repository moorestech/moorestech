using System;

namespace Game.Train.SaveLoad
{
    [Serializable]
    public class RailSegmentSaveData
    {
        // レール1セグメント分のセーブデータ（両端接続点・長さ・種別）
        // Save data for a rail segment
        public ConnectionDestination A;
        public ConnectionDestination B;
        public int Length;
        public Guid RailTypeGuid;
        public bool IsDrawable;
    }
}
