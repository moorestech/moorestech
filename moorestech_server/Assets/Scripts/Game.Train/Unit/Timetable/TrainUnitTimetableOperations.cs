using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.Unit
{
    // 列車状態と時刻表の置換を同じ操作で整合させる
    // Keep train state consistent with a timetable replacement in one operation
    public static class TrainUnitTimetableOperations
    {
        public static void ReplaceTimetable(this TrainUnit train, IReadOnlyList<IRailNode> stationNodes)
        {
            train.trainDiagram.ReplaceEntries(stationNodes);

            // 旧駅での停車を終え、新しい先頭駅への走行経路を求め直す
            // Leave the old station and recalculate the route to the new first stop
            if (train.trainUnitStationDocking.IsDocked)
            {
                train.trainUnitStationDocking.UndockFromStation();
            }
            if (train.IsAutoRun)
            {
                train.DiagramValidation(true);
            }
        }
    }
}
