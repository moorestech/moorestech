using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost
{
    public class TrainTimetableDtoBuilderTest
    {
        [Test]
        public void StationNameFallsBackToEmptyStringNotNull()
        {
            var dto = TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 2, 3), null);
            Assert.AreEqual(string.Empty, dto.Name);
            Assert.AreEqual(1, dto.Position.X);
            Assert.AreEqual(2, dto.Position.Y);
            Assert.AreEqual(3, dto.Position.Z);
        }

        [Test]
        public void StationsAreSortedByPositionForStableUi()
        {
            var stations = new List<TrainTimetableStationDto>
            {
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(5, 0, 0), "b"),
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 0, 9), "a"),
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 0, 2), "c"),
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, -2, 2), "d"),
            };
            TrainTimetableDtoBuilder.SortStations(stations);
            Assert.AreEqual("d", stations[0].Name);
            Assert.AreEqual("c", stations[1].Name);
            Assert.AreEqual("a", stations[2].Name);
            Assert.AreEqual("b", stations[3].Name);
        }

        // データストアの時刻表から停車駅の端・駅名・運転状態を写す
        // Map stop sides, names, and run state from the datastore timetable
        [Test]
        public void StopsCarrySideAndNameFromDatastore()
        {
            var id = TrainUnitInstanceId.Create();
            var stops = new[]
            {
                new TrainTimetableStop(new Vector3Int(1, 0, 0), StationNodeSide.Front),
                new TrainTimetableStop(new Vector3Int(9, 0, 0), StationNodeSide.Back),
            };
            var timetable = new TrainTimetableSnapshot(id, true, 1, stops);
            var stations = new List<TrainTimetableStationDto> { TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 0, 0), "north") };

            // 駅ブロックが引けない停車駅は無言で空名にせず警告を出す
            // A stop whose station block is missing warns instead of silently going nameless
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableDto\] station block not found"));
            var dto = TrainTimetableDtoBuilder.CreateFromTimetable(timetable, stations);

            Assert.That(dto.TrainUnitId, Is.EqualTo(id.ToString()));
            Assert.That(dto.IsAutoRun, Is.True);
            Assert.That(dto.CurrentIndex, Is.EqualTo(1));
            Assert.That(dto.Stops[0].Side, Is.EqualTo("front"));
            Assert.That(dto.Stops[0].Name, Is.EqualTo("north"));
            Assert.That(dto.Stops[1].Side, Is.EqualTo("back"));
            Assert.That(dto.Stops[1].Name, Is.EqualTo(string.Empty));
            Assert.That(dto.Stops[1].Position.X, Is.EqualTo(9));
            Assert.That(dto.Stations, Is.SameAs(stations));
        }
    }
}
