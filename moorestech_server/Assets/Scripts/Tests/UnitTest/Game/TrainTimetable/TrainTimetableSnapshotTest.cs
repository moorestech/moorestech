using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainTimetable
{
    public class TrainTimetableSnapshotTest
    {
        [Test]
        public void SnapshotCarriesTimetableAndAutoRunWithoutChangingHash()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var before = TrainUnitSnapshotHashCalculator.Compute(new[] { TrainUnitSnapshotFactory.CreateSnapshot(train) });

            // 駅付きノードへの置換は通知情報だけを変える
            // Replacing a stop with a station node changes notification data only
            train.trainDiagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });
            var bundle = TrainUnitSnapshotFactory.CreateSnapshot(train);

            Assert.IsTrue(bundle.Simulation.IsAutoRun);
            Assert.AreEqual(0, bundle.Simulation.TimetableCurrentIndex);
            Assert.AreEqual(1, bundle.Simulation.TimetableStops.Count);
            Assert.AreEqual(Vector3Int.zero, bundle.Simulation.TimetableStops[0].StationPosition);
            Assert.AreEqual(before, TrainUnitSnapshotHashCalculator.Compute(new[] { bundle }));
        }

        [Test]
        public void MessagePackRoundTripKeepsTimetable()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });
            var bundle = TrainUnitSnapshotFactory.CreateSnapshot(scenario.Train);

            // 通信形式を往復し、列車の時刻表情報を照合する
            // Round trip the wire format and compare the train timetable data
            var bytes = MessagePackSerializer.Serialize(new TrainUnitSnapshotBundleMessagePack(bundle));
            var restored = MessagePackSerializer.Deserialize<TrainUnitSnapshotBundleMessagePack>(bytes).ToModel();

            Assert.AreEqual(bundle.Simulation.IsAutoRun, restored.Simulation.IsAutoRun);
            Assert.AreEqual(bundle.Simulation.TimetableCurrentIndex, restored.Simulation.TimetableCurrentIndex);
            Assert.AreEqual(1, restored.Simulation.TimetableStops.Count);
            Assert.AreEqual(Vector3Int.zero, restored.Simulation.TimetableStops[0].StationPosition);
        }

        [Test]
        public void EmptyTimetableRoundTripKeepsEmptyStopsAndCursor()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode>());
            var bundle = TrainUnitSnapshotFactory.CreateSnapshot(scenario.Train);

            var bytes = MessagePackSerializer.Serialize(new TrainUnitSnapshotBundleMessagePack(bundle));
            var restored = MessagePackSerializer.Deserialize<TrainUnitSnapshotBundleMessagePack>(bytes).ToModel();

            Assert.AreEqual(-1, restored.Simulation.TimetableCurrentIndex);
            Assert.IsEmpty(restored.Simulation.TimetableStops);
            Assert.AreEqual(bundle.Simulation.IsAutoRun, restored.Simulation.IsAutoRun);
        }
    }
}
