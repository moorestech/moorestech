using System.Collections.Generic;
using Client.WebUiHost.Game.Topics.BlockDetail;
using NUnit.Framework;
using UnityEngine;

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
    }
}
