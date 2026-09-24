using System.Collections.Generic;
using Game.Train.RailGraph;
using UnityEngine;

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
            if (train.IsAutoRun && stationNodes.Count > 0)
            {
                var approaching = train.RailPosition.GetNodeApproaching();
                var destination = stationNodes[0];
                var railNodes = train.RailPosition.GetRailNodes();
                var reverseApproaching = railNodes[railNodes.Count - 1].OppositeNode;

                // 未接続駅も受理し、経路がある場合だけ置換直後に再検証する
                // Accept disconnected stations and revalidate immediately only when a route exists
                if (approaching == destination || HasRoute(approaching, destination) || HasRoute(reverseApproaching, destination))
                {
                    train.DiagramValidation(true);
                }
                else
                {
                    Debug.LogWarning($"[TrainTimetable] no route to replacement head train={train.TrainUnitInstanceId}");
                }
            }

            #region Internal

            bool HasRoute(IRailNode start, IRailNode end)
            {
                return start != null && start.GraphProvider.FindShortestPath(start, end)?.Count >= 2;
            }

            #endregion
        }
    }
}
