using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Train.Timetable
{
    public class TrainTimetableReadyDataFactoryTest
    {
        private static readonly List<TrainTimetableStation> Stations = new() { TrainTestData.Unnamed, TrainTestData.North };

        [Test]
        public void 停車駅の名前は駅一覧から引き引けない位置は空名で警告は集合が変わったときだけ()
        {
            var factory = new TrainTimetableReadyDataFactory();
            var snapshot = Snapshot(StopAt(TrainTestData.North.Position), StopAt(new Vector3Int(99, 0, 0)));

            // 初回は欠けの集合が変わるので 1 回警告する
            // The first build changes the missing set, so it warns once
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableModel\] station blocks not found.*99"));
            Assert.IsTrue(factory.TryCreate(snapshot, Stations, out var data));
            Assert.AreEqual("北駅", data.Stops[0].Name);
            Assert.AreEqual(string.Empty, data.Stops[1].Name);

            // 同じ集合ではもう警告しない
            // The same set does not warn again
            Assert.IsTrue(factory.TryCreate(snapshot, Stations, out _));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 未知のSideがあれば作らずErrorログ()
        {
            var factory = new TrainTimetableReadyDataFactory();
            var snapshot = Snapshot(new TrainTimetableStop(TrainTestData.North.Position, (StationNodeSide)99, TrainDiagram.DepartureConditionType.WaitForTicks, 20));
            LogAssert.Expect(LogType.Error, new Regex(@"\[TrainTimetableModel\] discarding timetable with unknown side"));
            Assert.IsFalse(factory.TryCreate(snapshot, Stations, out _));
        }

        [Test]
        public void 中身が同じ用意済みは流し直さず読込中と使えないは同じ値で流さない()
        {
            // 貨車の中身の更新だけでは時刻表の行を作り直さない（R-5）
            // A car contents update alone does not rebuild the timetable rows (R-5)
            var first = TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1));
            var same = TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1));
            Assert.IsTrue(TrainTimetableViewState.IsSameState(first, same));

            // 自動運転だけ・停車駅だけ・現在位置だけ・駅一覧だけが違っても流し直す
            // A difference in only auto-run, only the stops, only the current index, or only the station list still republishes
            var northOnly = TrainTestData.NorthOnly(TrainTestData.Unit1);
            var autoRunOnly = TrainTimetableViewState.Ready(TrainTestData.Ready(TrainTestData.Unit1, true, TrainTestData.Added(TrainTestData.NoStops, TrainTestData.North)));
            var stopsOnly = TrainTimetableViewState.Ready(TrainTestData.Ready(TrainTestData.Unit1, false, TrainTestData.Added(TrainTestData.Added(TrainTestData.NoStops, TrainTestData.North), TrainTestData.Unnamed)));
            var indexOnly = TrainTimetableViewState.Ready(new TrainTimetableReadyData(TrainTestData.Unit1, false, 1, northOnly.Stops, northOnly.Stations));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, autoRunOnly));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, stopsOnly));
            var stationsOnly = TrainTimetableViewState.Ready(new TrainTimetableReadyData(TrainTestData.Unit1, false, 0, northOnly.Stops, new List<TrainTimetableStation> { TrainTestData.North }));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, indexOnly));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, stationsOnly));
            Assert.IsTrue(TrainTimetableViewState.IsSameState(TrainTimetableViewState.Loading, TrainTimetableViewState.Loading));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, TrainTimetableViewState.Loading));
        }

        private static TrainTimetableStop StopAt(Vector3Int position)
        {
            return new TrainTimetableStop(position, StationNodeSide.Back, TrainDiagram.DepartureConditionType.WaitForTicks, 20);
        }

        private static TrainTimetableSnapshot Snapshot(params TrainTimetableStop[] stops)
        {
            return new TrainTimetableSnapshot(TrainTestData.Unit1, false, 0, stops);
        }
    }
}
