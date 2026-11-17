using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Ui.State.Diagnostics;
using Client.Ui.State.Train.Logic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 時刻表のスナップショットと駅一覧から用意済みの時刻表を組む唯一の口（TrainTimetableModel が呼ぶ）
    /// The single builder of the ready timetable from a snapshot and the station list (called by TrainTimetableModel)
    /// </summary>
    public sealed class TrainTimetableReadyDataFactory
    {
        private readonly Dictionary<TrainUnitInstanceId, HashSet<Vector3Int>> _lastWarnedMissingStationPositions = new();
        private readonly Dictionary<TrainUnitInstanceId, AnomalyReportGate> _unknownSideGates = new();
        private readonly TrainTimetableMissingCarReport _missingCarReport = new();

        // 開いている貨車の時刻表の状態の唯一の優先順（TrainTimetableModel が呼ぶ）。列車不明>不可>取得中>無>用意済
        // The single priority order of the open car's timetable state (called by TrainTimetableModel): no train, unavailable, in flight, no cache, ready
        public TrainTimetableViewState Build(long trainCarInstanceId, TrainUnitClientCache cache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher, BlockGameObjectDataStore blocks)
        {
            if (!OpenTrainUnitResolver.TryResolveOwningTrain(trainCarInstanceId, cache, out var trainUnitInstanceId))
            {
                _missingCarReport.ReportMissing(trainCarInstanceId);
                return TrainTimetableViewState.Unavailable;
            }
            _missingCarReport.Clear();
            if (fetcher.IsUnavailable(trainUnitInstanceId)) return TrainTimetableViewState.Unavailable;
            if (fetcher.IsFetchInFlight(trainUnitInstanceId)) return TrainTimetableViewState.Loading;
            if (!timetables.TryGet(trainUnitInstanceId, out var timetable)) return TrainTimetableViewState.Loading;
            return TryCreate(timetable, TrainTimetableStationCatalog.ReadSorted(blocks), out var data)
                ? TrainTimetableViewState.Ready(data)
                : TrainTimetableViewState.Unavailable;
        }

        // 貨車が無い間は旗を戻し再警告に備える
        // Clear the flag while no car is open so it warns again
        public void ClearMissingCarReport()
        {
            _missingCarReport.Clear();
        }


        public bool TryCreate(TrainTimetableSnapshot timetable, IReadOnlyList<TrainTimetableStation> stations, out TrainTimetableReadyData data)
        {
            data = null;
            var stationNames = new Dictionary<Vector3Int, string>();
            foreach (var station in stations) stationNames[station.Position] = station.Name;

            // 停車駅ごとに駅名を当て、未知の端は Unavailable
            // Name each stop; unknown side makes it Unavailable
            var stops = new List<TimetableEditStop>(timetable.Stops.Count);
            var missingStationPositions = new HashSet<Vector3Int>();
            foreach (var stop in timetable.Stops)
            {
                if (!Enum.IsDefined(typeof(StationNodeSide), stop.Side))
                {
                    // 同じ時刻表の破棄は 1 回だけ出す
                    // Report once per discarded timetable
                    // 旗は列車ごとに異常の間だけ持つ。壊れた 2 列車を交互に処理しても連打にならず、正常な列車の分は残らない
                    // Keep the flag per train only while it is anomalous: two broken trains handled alternately do not spam, and healthy trains leave nothing behind
                    var unknownSide = $"[TrainTimetableModel] discarding timetable with unknown side: {timetable.TrainUnitInstanceId}";
                    if (!_unknownSideGates.TryGetValue(timetable.TrainUnitInstanceId, out var unknownSideGate))
                    {
                        unknownSideGate = new AnomalyReportGate();
                        _unknownSideGates[timetable.TrainUnitInstanceId] = unknownSideGate;
                    }
                    if (unknownSideGate.ShouldReport(unknownSide)) Debug.LogError(unknownSide);
                    // 捨てた時刻表は欠けの比較の元にしない。届き直したら同じ欠けでも警告し直す
                    // A discarded timetable is no baseline for missing stations, so a re-delivered one warns again even with the same set
                    _lastWarnedMissingStationPositions.Remove(timetable.TrainUnitInstanceId);
                    return false;
                }
                if (!stationNames.TryGetValue(stop.StationPosition, out var name)) missingStationPositions.Add(stop.StationPosition);
                stops.Add(new TimetableEditStop(stop.StationPosition, name ?? string.Empty, stop.Side));
            }
            _unknownSideGates.Remove(timetable.TrainUnitInstanceId);
            WarnMissingStationsWhenChanged(timetable.TrainUnitInstanceId, missingStationPositions);

            data = new TrainTimetableReadyData(timetable.TrainUnitInstanceId, timetable.IsAutoRun, timetable.CurrentIndex, stops, stations);
            return true;

            #region Internal

            // 欠けの集合が同じ列車の前回と同じなら黙る。旗は列車ごとに欠けの間だけ持ち、別の列車を挟んでも警告が出直さない
            // Stay quiet when the missing set equals the same train's last one; the flag is per train and only while stations are missing, so interleaving trains does not re-warn
            void WarnMissingStationsWhenChanged(TrainUnitInstanceId trainUnitInstanceId, HashSet<Vector3Int> missingStationPositions)
            {
                if (missingStationPositions.Count == 0)
                {
                    _lastWarnedMissingStationPositions.Remove(trainUnitInstanceId);
                    return;
                }
                if (_lastWarnedMissingStationPositions.TryGetValue(trainUnitInstanceId, out var lastWarned) && lastWarned.SetEquals(missingStationPositions)) return;
                _lastWarnedMissingStationPositions[trainUnitInstanceId] = missingStationPositions;
                Debug.LogWarning($"[TrainTimetableModel] station blocks not found for stops at {string.Join(", ", missingStationPositions)}; emitting empty names: {trainUnitInstanceId}");
            }

            #endregion
        }
    }
}
