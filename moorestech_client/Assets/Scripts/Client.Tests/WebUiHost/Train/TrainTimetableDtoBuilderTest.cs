using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost
{
    public class TrainTimetableDtoBuilderTest
    {
        // State の組み立て器が作った時刻表から、停車駅の端・駅名・運転状態・駅一覧を wire の DTO へ写す
        // Map stop sides, names, run state and the station list from the State-built timetable to the wire DTO
        [Test]
        public void StopsCarrySideAndNameFromDatastore()
        {
            var id = TrainUnitInstanceId.Create();
            var factory = new TrainTimetableReadyDataFactory();
            var stops = new[]
            {
                new TrainTimetableStop(new Vector3Int(1, 0, 0), StationNodeSide.Front, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond),
                new TrainTimetableStop(new Vector3Int(9, 0, 0), StationNodeSide.Back, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond),
            };
            var timetable = new TrainTimetableSnapshot(id, true, 1, stops);
            var stations = new List<TrainTimetableStation> { new(new Vector3Int(1, 0, 0), "north") };

            // 駅ブロックが引けない停車駅は無言で空名にせず警告を出す
            // A stop whose station block is missing warns instead of silently going nameless
            LogAssert.Expect(LogType.Warning, new Regex(@"station blocks not found"));
            Assert.IsTrue(factory.TryCreate(timetable, stations, out var data));
            var dto = TrainTimetableDtoBuilder.ToDto(data);

            Assert.That(dto.TrainUnitId, Is.EqualTo(id.ToString()));
            Assert.That(dto.IsAutoRun, Is.True);
            Assert.That(dto.CurrentIndex, Is.EqualTo(1));
            Assert.That(dto.Stops[0].Side, Is.EqualTo("front"));
            Assert.That(dto.Stops[0].Name, Is.EqualTo("north"));
            Assert.That(dto.Stops[1].Side, Is.EqualTo("back"));
            Assert.That(dto.Stops[1].Name, Is.EqualTo(string.Empty));
            Assert.That(dto.Stops[1].Position.X, Is.EqualTo(9));
            Assert.That(dto.Stations.Count, Is.EqualTo(1));
            Assert.That(dto.Stations[0].Name, Is.EqualTo("north"));
            Assert.That(dto.Stations[0].Position.X, Is.EqualTo(1));
        }
    }
}
