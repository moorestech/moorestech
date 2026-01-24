using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;

namespace Game.Train.RailPositions
{
    // レール位置のセーブデータ
    // Save data for rail positions
    [Serializable]
    public class RailPositionSaveData
    {
        public int TrainLength { get; set; }
        public int DistanceToNextNode { get; set; }
        public List<ConnectionDestination> RailSnapshot { get; set; }
    }
}
