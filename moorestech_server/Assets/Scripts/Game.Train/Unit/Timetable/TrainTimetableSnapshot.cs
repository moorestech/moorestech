using System.Collections.Generic;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Unit
{
    // 停車駅の駅ブロック原点と入線する端
    // A stop's station block origin and the side the train arrives at
    public readonly struct TrainTimetableStop
    {
        public TrainTimetableStop(Vector3Int stationPosition, StationNodeSide side)
        {
            StationPosition = stationPosition;
            Side = side;
        }

        public Vector3Int StationPosition { get; }
        public StationNodeSide Side { get; }
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
            // 駅参照を持つ停車駅だけを座標と端で送る
            // Send only stops with a station reference, as position and side
            var entries = train.trainDiagram.Entries;
            var stops = new List<TrainTimetableStop>(entries.Count);
            var currentStopIndex = -1;
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var station = entries[entryIndex].Node.StationRef;
                if (station == null || !station.HasStation) continue;
                if (entryIndex == train.trainDiagram.CurrentIndex) currentStopIndex = stops.Count;
                stops.Add(new TrainTimetableStop(station.StationPosition, station.NodeSide));
            }
            return new TrainTimetableSnapshot(train.TrainUnitInstanceId, train.IsAutoRun, currentStopIndex, stops);
        }
    }
}
