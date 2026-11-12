using System;
using System.Collections.Generic;
using Client.Ui.State.Diagnostics;
using Client.Ui.State.Train.Logic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 時刻表のスナップショットと駅一覧から用意済みの時刻表を組む（Web ホスト CreateFromTimetable と同じ規則。D-2）
    /// Builds the ready timetable from a snapshot and the station list (same rule as the Web host CreateFromTimetable; D-2)
    /// </summary>
    public sealed class TrainTimetableReadyDataFactory
    {
        private readonly HashSet<Vector3Int> _lastWarnedMissingStationPositions = new();
        private readonly Dictionary<TrainUnitInstanceId, AnomalyReportGate> _unknownSideGates = new();

        public bool TryCreate(TrainTimetableSnapshot timetable, IReadOnlyList<TrainTimetableStation> stations, out TrainTimetableReadyData data)
        {
            data = null;
            var stationNames = new Dictionary<Vector3Int, string>();
            foreach (var station in stations) stationNames[station.Position] = station.Name;

            // 停車駅ごとに駅名を当て、未知の端は Unavailable
            // Name each stop; unknown side makes it Unavailable
            // 旗は列車ごとに持つ。壊れた 2 列車を交互に処理しても連打にならない
            // Keep the flag per train so two broken trains handled alternately do not spam
            if (!_unknownSideGates.TryGetValue(timetable.TrainUnitInstanceId, out var unknownSideGate))
            {
                unknownSideGate = new AnomalyReportGate();
                _unknownSideGates[timetable.TrainUnitInstanceId] = unknownSideGate;
            }
            var stops = new List<TimetableEditStop>(timetable.Stops.Count);
            var missingStationPositions = new HashSet<Vector3Int>();
            foreach (var stop in timetable.Stops)
            {
                if (!Enum.IsDefined(typeof(StationNodeSide), stop.Side))
                {
                    // 同じ時刻表を捨てている間は 1 回だけ出す
                    // Report once while the same timetable keeps being discarded
                    var unknownSide = $"[TrainTimetableModel] discarding timetable with unknown side: {timetable.TrainUnitInstanceId}";
                    if (unknownSideGate.ShouldReport(unknownSide)) Debug.LogError(unknownSide);
                    return false;
                }
                if (!stationNames.TryGetValue(stop.StationPosition, out var name)) missingStationPositions.Add(stop.StationPosition);
                stops.Add(new TimetableEditStop(stop.StationPosition, name ?? string.Empty, stop.Side));
            }
            unknownSideGate.ShouldReport(null);
            WarnMissingStationsWhenChanged(timetable.TrainUnitInstanceId, missingStationPositions);

            data = new TrainTimetableReadyData(timetable.TrainUnitInstanceId, timetable.IsAutoRun, timetable.CurrentIndex, stops, stations);
            return true;
        }

        // 欠けの集合が前回と同じなら黙る。公開のたびに同じ警告を出さないため（Web ホストと同じ）
        // Stay quiet when the missing set is unchanged so each publish does not repeat the warning (same as the Web host)
        private void WarnMissingStationsWhenChanged(TrainUnitInstanceId trainUnitInstanceId, HashSet<Vector3Int> missingStationPositions)
        {
            if (_lastWarnedMissingStationPositions.SetEquals(missingStationPositions)) return;
            _lastWarnedMissingStationPositions.Clear();
            _lastWarnedMissingStationPositions.UnionWith(missingStationPositions);
            if (missingStationPositions.Count == 0) return;
            Debug.LogWarning($"[TrainTimetableModel] station blocks not found for stops at {string.Join(", ", missingStationPositions)}; emitting empty names: {trainUnitInstanceId}");
        }
    }
}
