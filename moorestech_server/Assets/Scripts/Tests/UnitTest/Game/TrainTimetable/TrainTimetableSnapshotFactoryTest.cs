using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainTimetable
{
    public class TrainTimetableSnapshotFactoryTest
    {
        [Test]
        public void CarriesAutoRunCursorAndStopSide()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });

            var snapshot = TrainTimetableSnapshotFactory.Create(scenario.Train);

            Assert.AreEqual(scenario.Train.TrainUnitInstanceId, snapshot.TrainUnitInstanceId);
            Assert.IsTrue(snapshot.IsAutoRun);
            Assert.AreEqual(0, snapshot.CurrentIndex);
            Assert.AreEqual(1, snapshot.Stops.Count);
            Assert.AreEqual(Vector3Int.zero, snapshot.Stops[0].StationPosition);
            Assert.AreEqual(StationNodeSide.Front, snapshot.Stops[0].Side);
        }

        [Test]
        public void EmptyTimetableHasNoCursor()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode>());

            var snapshot = TrainTimetableSnapshotFactory.Create(scenario.Train);

            Assert.AreEqual(-1, snapshot.CurrentIndex);
            Assert.IsEmpty(snapshot.Stops);
        }

        [Test]
        public void NonStationEntryDoesNotHighlightAnotherStation()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.MoveToNextEntry();

            var snapshot = TrainTimetableSnapshotFactory.Create(scenario.Train);

            Assert.AreEqual(-1, snapshot.CurrentIndex);
            Assert.AreEqual(1, snapshot.Stops.Count);
        }
    }
}
