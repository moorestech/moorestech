using System.Collections.Generic;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Unit
{
    // 停車駅の駅ブロック原点・入線する端・出発条件
    // A stop's station block origin, the side the train arrives at, and its departure condition
    public readonly struct TrainTimetableStop
    {
        public TrainTimetableStop(Vector3Int stationPosition, StationNodeSide side, TrainDiagram.DepartureConditionType departureConditionType, int waitTicks)
        {
            StationPosition = stationPosition;
            Side = side;
            DepartureConditionType = departureConditionType;
            WaitTicks = waitTicks;
        }

        public Vector3Int StationPosition { get; }
        public StationNodeSide Side { get; }
        public TrainDiagram.DepartureConditionType DepartureConditionType { get; }
        public int WaitTicks { get; }
    }

    // UIへ渡す列車1編成の時刻表と自動運転状態（tick同期しない）
    // One train's timetable and auto-run state for the UI (not tick-synchronized)
    public readonly struct TrainTimetableSnapshot
    {
        public TrainTimetableSnapshot(TrainUnitInstanceId trainUnitInstanceId, bool isAutoRun, int currentIndex, IReadOnlyList<TrainTimetableStop> stops)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            IsAutoRun = isAutoRun;
            CurrentIndex = currentIndex;
            Stops = stops;
        }

        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public bool IsAutoRun { get; }
        public int CurrentIndex { get; }
        public IReadOnlyList<TrainTimetableStop> Stops { get; }
    }

    public static class TrainTimetableSnapshotFactory
    {
        public static TrainTimetableSnapshot Create(TrainUnit train)
        {
            // 駅参照を持つ停車駅だけを座標と端と出発条件で送る
            // Send only stops with a station reference, as position, side and departure condition
            var entries = train.trainDiagram.Entries;
            var stops = new List<TrainTimetableStop>(entries.Count);
            var currentStopIndex = -1;
            var skippedEntryCount = 0;
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                var station = entry.Node.StationRef;
                if (station == null || !station.HasStation)
                {
                    // 駅でないentryはUIに置き場が無いので落とす。件数を残して無音の欠落を避ける
                    // Non-station entries have no place in the UI, so drop them and log the count instead of dropping silently
                    skippedEntryCount++;
                    continue;
                }
                if (entryIndex == train.trainDiagram.CurrentIndex) currentStopIndex = stops.Count;
                var (departureConditionType, waitTicks) = ResolveDepartureCondition(entry);
                stops.Add(new TrainTimetableStop(station.StationPosition, station.NodeSide, departureConditionType, waitTicks));
            }
            if (0 < skippedEntryCount)
            {
                Debug.LogWarning($"[TrainTimetableSnapshot] 駅参照の無いentryをUIへ送らず落とした train={train.TrainUnitInstanceId} skipped={skippedEntryCount} entries={entries.Count} currentIndex={train.trainDiagram.CurrentIndex}");
            }
            return new TrainTimetableSnapshot(train.TrainUnitInstanceId, train.IsAutoRun, currentStopIndex, stops);
        }

        // entryの実際の出発条件を1件だけ取り出す。条件なしは待機0tick（即発車）として表す
        // Take the entry's single actual departure condition; no condition is expressed as a zero-tick wait
        private static (TrainDiagram.DepartureConditionType departureConditionType, int waitTicks) ResolveDepartureCondition(TrainDiagramEntry entry)
        {
            var waitTicks = entry.GetWaitForTicksInitialTicks();
            if (waitTicks.HasValue)
            {
                return (TrainDiagram.DepartureConditionType.WaitForTicks, waitTicks.Value);
            }
            if (0 < entry.DepartureConditionTypes.Count)
            {
                return (entry.DepartureConditionTypes[0], 0);
            }
            return (TrainDiagram.DepartureConditionType.WaitForTicks, 0);
        }
    }
}
