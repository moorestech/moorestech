using System;
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// Web のモック trainWithTimetable と同じ駅を持ち、停車駅は L0-s4b の編集ロジックで組む（Side を手で書かない）
    /// Holds the same stations as the Web mock trainWithTimetable and builds stops with the L0-s4b edit logic (never hand-written sides)
    /// </summary>
    public static class TrainTestData
    {
        // TrainUnitInstanceId は固定の Guid で組む
        // TrainUnitInstanceId is built from fixed Guids
        public static readonly TrainUnitInstanceId Unit1 = new TrainUnitInstanceId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        public static readonly TrainUnitInstanceId Unit2 = new TrainUnitInstanceId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        public static readonly TrainTimetableStation Unnamed = new(new Vector3Int(-8, 0, 3), string.Empty);
        public static readonly TrainTimetableStation North = new(new Vector3Int(12, 0, 40), "北駅");
        public static readonly IReadOnlyList<TimetableEditStop> NoStops = new List<TimetableEditStop>();

        // 追加した駅の並び。Side は AddStop が決める（Back 固定）
        // Order after adding a station; AddStop decides the side (always Back)
        public static IReadOnlyList<TimetableEditStop> Added(IReadOnlyList<TimetableEditStop> stops, TrainTimetableStation station)
        {
            return TimetableEditLogic.AddStop(stops, station);
        }

        public static TrainTimetableReadyData Ready(TrainUnitInstanceId unit, bool isAutoRun, IReadOnlyList<TimetableEditStop> stops)
        {
            return new TrainTimetableReadyData(unit, isAutoRun, 0, stops, new List<TrainTimetableStation> { Unnamed, North });
        }

        public static TrainTimetableReadyData NorthOnly(TrainUnitInstanceId unit)
        {
            return Ready(unit, false, Added(NoStops, North));
        }
    }
}
