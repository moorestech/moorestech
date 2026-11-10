using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableDepartureTest
    {
        [Test]
        public void 送る停車駅の出発条件は1秒待ち固定()
        {
            // Web ホスト TrainTimetableStopParser と同じ固定の出発条件
            // Same fixed departure condition as the Web host TrainTimetableStopParser
            var stops = TrainTimetableDeparture.ToServerStops(new[] { new TimetableReplaceStop(new Vector3Int(1, 2, 3), StationNodeSide.Front) });
            Assert.AreEqual(new Vector3Int(1, 2, 3), stops[0].StationPosition);
            Assert.AreEqual(StationNodeSide.Front, stops[0].Side);
            Assert.AreEqual(TrainDiagram.DepartureConditionType.WaitForTicks, stops[0].DepartureConditionType);
            Assert.AreEqual(GameUpdater.TicksPerSecond, stops[0].WaitTicks);
        }
    }
}
