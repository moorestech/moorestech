using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using NUnit.Framework;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableAreaViewModelTest
    {
        [Test]
        public void 区画は比較項目が1つでも違えば同じ中身とみなさない()
        {
            var server = NorthOnly(Unit1);
            var baseline = TrainTimetableSectionViewModel.Build(server, server.Stops, false);
            Assert.IsTrue(baseline.HasSameContent(TrainTimetableSectionViewModel.Build(server, server.Stops, false)));

            // 保留中だけ・自動運転だけ・現在位置だけ・駅一覧だけが違う
            // Only pending, only auto-run, only the current index, or only the station list differs
            Assert.IsFalse(baseline.HasSameContent(TrainTimetableSectionViewModel.Build(server, server.Stops, true)));
            var autoRun = new TrainTimetableReadyData(Unit1, true, 0, server.Stops, server.Stations);
            Assert.IsFalse(baseline.HasSameContent(TrainTimetableSectionViewModel.Build(autoRun, server.Stops, false)));
            var index = new TrainTimetableReadyData(Unit1, false, 1, server.Stops, server.Stations);
            Assert.IsFalse(baseline.HasSameContent(TrainTimetableSectionViewModel.Build(index, server.Stops, false)));
            var stations = new TrainTimetableReadyData(Unit1, false, 0, server.Stops, new List<TrainTimetableStation> { North });
            Assert.IsFalse(baseline.HasSameContent(TrainTimetableSectionViewModel.Build(stations, server.Stops, false)));
        }

        [Test]
        public void 区画は表示中の停車駅だけ違っても自動運転の可否だけ違っても同じ中身とみなさない()
        {
            // 表示の並びがサーバーと違うと現在位置は両方 null になり、停車駅の比較だけが差を見る
            // When the displayed order differs from the server both current indexes are null, so only the stops comparison sees the difference
            var emptyServer = new TrainTimetableReadyData(Unit1, false, 0, NoStops, new List<TrainTimetableStation> { Unnamed, North });
            var northStops = Added(NoStops, North);
            var unnamedStops = Added(NoStops, Unnamed);
            var stopsA = TrainTimetableSectionViewModel.Build(emptyServer, northStops, false);
            Assert.IsFalse(stopsA.HasSameContent(TrainTimetableSectionViewModel.Build(emptyServer, unnamedStops, false)));

            // 表示の並びを同じにし、サーバーの停車駅の有無だけで自動運転の可否が変わる
            // Same displayed stops; only the server stops decide whether auto-run can be enabled
            var withStops = new TrainTimetableReadyData(Unit1, false, 0, unnamedStops, emptyServer.Stations);
            var canEnable = TrainTimetableSectionViewModel.Build(withStops, northStops, false);
            Assert.IsFalse(stopsA.HasSameContent(canEnable));
        }

        [Test]
        public void 領域は列車か区画の中身が違えば同じ領域とみなさない()
        {
            var server = NorthOnly(Unit1);
            var section = TrainTimetableSectionViewModel.Build(server, server.Stops, false);
            var area = TrainTimetableAreaViewModel.Ready(Unit1, section);
            Assert.IsTrue(TrainTimetableAreaViewModel.IsSameArea(area, TrainTimetableAreaViewModel.Ready(Unit1, section)));
            Assert.IsFalse(TrainTimetableAreaViewModel.IsSameArea(area, TrainTimetableAreaViewModel.Ready(Unit2, section)));
            Assert.IsFalse(TrainTimetableAreaViewModel.IsSameArea(area, TrainTimetableAreaViewModel.Ready(Unit1, TrainTimetableSectionViewModel.Build(server, server.Stops, true))));
            Assert.IsFalse(TrainTimetableAreaViewModel.IsSameArea(area, TrainTimetableAreaViewModel.Loading));
            Assert.IsTrue(TrainTimetableAreaViewModel.IsSameArea(TrainTimetableAreaViewModel.Loading, TrainTimetableAreaViewModel.Loading));
        }
    }
}
