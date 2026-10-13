using System.Collections.Generic;
using Game.Train.Diagram;

namespace Game.Train.Unit
{
    // 列車状態と時刻表の置換を同じ操作で整合させる
    // Keep train state consistent with a timetable replacement in one operation
    public static class TrainUnitTimetableOperations
    {
        // 時刻表を置き換える唯一の公開入口。置換に伴う複数の状態変化は1回の通知へまとめる
        // The only public entry for replacing a timetable; its several state changes coalesce into one notification
        public static void ReplaceTimetable(this TrainUnit train, IReadOnlyList<TrainDiagramStopPlan> stops)
        {
            train.BeginTimetableChangeBatch();
            train.trainDiagram.ReplaceEntries(stops);

            // 不変条件: 置換したら必ず離線する（旧駅の停車は新しい時刻表のどのentryにも属さないため）
            // Invariant: a replacement always undocks, since the old docking belongs to no entry of the new timetable
            if (train.trainUnitStationDocking.IsDocked)
            {
                train.trainUnitStationDocking.UndockFromStation();
            }
            if (train.IsAutoRun)
            {
                train.DiagramValidation(true);
            }
            train.EndTimetableChangeBatch(true);
        }
    }
}
