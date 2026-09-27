using System.Collections.Generic;
using Core.Update;
using Game.Context;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramReplaceEntriesTest
    {
        // 1秒待機の停車駅指定を組み立てる
        // Build stop plans that wait one second at each station
        private static IReadOnlyList<TrainDiagramStopPlan> Stops(params IRailNode[] nodes)
        {
            var stops = new List<TrainDiagramStopPlan>(nodes.Length);
            foreach (var node in nodes)
            {
                stops.Add(new TrainDiagramStopPlan(node, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond));
            }
            return stops;
        }

        [Test]
        public void ReplaceTimetableResetsToHeadAndKeepsAutoRun()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;
            var diagram = train.trainDiagram;
            Assert.IsTrue(train.IsAutoRun);
            Assert.Less(1, diagram.Entries.Count);

            // 別のノードから始まる順序へ置き換える
            // Replace with an order starting at another node
            var nodes = new[] { diagram.Entries[1].Node, diagram.Entries[0].Node };
            var notifications = 0;
            using var subscription = ServerContext.GetService<ITrainTimetableNotifyEvent>().OnTimetableChanged
                .Subscribe(notified =>
                {
                    if (notified.TrainUnitInstanceId == train.TrainUnitInstanceId) notifications++;
                });

            train.ReplaceTimetable(Stops(nodes));

            Assert.AreEqual(2, diagram.Entries.Count);
            Assert.AreEqual(0, diagram.CurrentIndex);
            Assert.AreSame(nodes[0], diagram.GetCurrentNode());
            Assert.IsTrue(train.IsAutoRun);
            Assert.AreEqual(GameUpdater.TicksPerSecond, diagram.Entries[0].GetWaitForTicksInitialTicks());
            Assert.AreEqual(GameUpdater.TicksPerSecond, diagram.Entries[1].GetWaitForTicksInitialTicks());
            Assert.AreEqual(1, notifications, "置換1回につき通知は1回だけ");
        }

        [Test]
        public void ReplacingWhileDockedHeadsForNewFirstStation()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var firstStation = scenario.AddConnectedDestinationStation();
            Assert.IsTrue(train.trainUnitStationDocking.IsDocked);

            train.ReplaceTimetable(Stops(firstStation, scenario.StationExitFront));
            Assert.IsFalse(train.trainUnitStationDocking.IsDocked);
            Assert.IsTrue(train.IsAutoRun);

            // 旧駅の待機時間を超えても新しい先頭駅を飛ばさない
            // Keep the new first station after the old docking wait would have expired
            var updateService = ServerContext.GetService<TrainUpdateService>();
            for (var i = 0; i < GameUpdater.TicksPerSecond + 2; i++)
            {
                updateService.UpdateTrains();
            }
            Assert.AreSame(firstStation, train.trainDiagram.GetCurrentNode());
            Assert.AreEqual(0, train.trainDiagram.CurrentIndex);
        }

        [Test]
        public void ReplaceTimetableWithEmptyListTurnsOffAutoRun()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;

            train.ReplaceTimetable(new List<TrainDiagramStopPlan>());

            Assert.AreEqual(0, train.trainDiagram.Entries.Count);
            Assert.AreEqual(-1, train.trainDiagram.CurrentIndex);
            Assert.IsNull(train.trainDiagram.GetCurrentNode());
            // 目的地が無くなった時点で自動運転は落ちる
            // Auto-run drops as soon as the destination is gone
            Assert.IsFalse(train.IsAutoRun);
        }

        [Test]
        public void MoveToNextEntryNotifiesOnlyWhenCursorMoves()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var diagram = train.trainDiagram;
            var notifications = 0;
            using var subscription = ServerContext.GetService<ITrainTimetableNotifyEvent>().OnTimetableChanged
                .Subscribe(notified =>
                {
                    if (notified.TrainUnitInstanceId == train.TrainUnitInstanceId) notifications++;
                });

            diagram.MoveToNextEntry();

            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void RemovingCurrentNodeNotifiesOnce()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var diagram = train.trainDiagram;
            var currentNode = diagram.GetCurrentNode();
            var notifications = 0;
            using var subscription = ServerContext.GetService<ITrainTimetableNotifyEvent>().OnTimetableChanged
                .Subscribe(notified =>
                {
                    if (notified.TrainUnitInstanceId == train.TrainUnitInstanceId) notifications++;
                });

            diagram.HandleNodeRemoval(currentNode);

            Assert.AreEqual(1, notifications);
            diagram.HandleNodeRemoval(currentNode);
            Assert.AreEqual(1, notifications, "消えたノードの再削除では通知しない");
        }

        [Test]
        public void SaveDataKeepsEntriesAfterReplace()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            train.ReplaceTimetable(Stops(scenario.StationExitFront));

            var saveData = train.trainDiagram.CreateTrainDiagramSaveData();

            Assert.AreEqual(1, saveData.Entries.Count);
            Assert.AreEqual(0, saveData.CurrentIndex);
            Assert.AreEqual(GameUpdater.TicksPerSecond, saveData.Entries[0].WaitForTicksInitial);
        }
    }
}
