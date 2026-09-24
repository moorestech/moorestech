using System.Collections.Generic;
using Core.Update;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramReplaceEntriesTest
    {
        [Test]
        public void ReplaceEntriesResetsToHeadAndKeepsAutoRun()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;
            var diagram = train.trainDiagram;
            Assert.IsTrue(train.IsAutoRun);
            Assert.Greater(diagram.Entries.Count, 1);

            // 別のノードから始まる順序へ置き換える
            // Replace with an order starting at another node
            var nodes = new List<IRailNode> { diagram.Entries[1].Node, diagram.Entries[0].Node };
            diagram.ReplaceEntries(nodes);

            Assert.AreEqual(2, diagram.Entries.Count);
            Assert.AreEqual(0, diagram.CurrentIndex);
            Assert.AreSame(nodes[0], diagram.GetCurrentNode());
            Assert.IsTrue(train.IsAutoRun);
            Assert.AreEqual(GameUpdater.TicksPerSecond, diagram.Entries[0].GetWaitForTicksInitialTicks());
            Assert.AreEqual(GameUpdater.TicksPerSecond, diagram.Entries[1].GetWaitForTicksInitialTicks());
            Assert.IsTrue(diagram.ConsumeCurrentEntryChanged());
            Assert.IsFalse(diagram.ConsumeCurrentEntryChanged());
        }

        [Test]
        public void ReplacingWhileDockedHeadsForNewFirstStation()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var firstStation = scenario.AddConnectedDestinationStation();
            Assert.IsTrue(train.trainUnitStationDocking.IsDocked);

            train.ReplaceTimetable(new List<IRailNode> { firstStation, scenario.StationExitFront });
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
        public void ReplaceEntriesWithEmptyListTurnsOffAutoRunOnNextUpdate()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;

            train.trainDiagram.ReplaceEntries(new List<IRailNode>());

            Assert.AreEqual(0, train.trainDiagram.Entries.Count);
            Assert.AreEqual(-1, train.trainDiagram.CurrentIndex);
            Assert.IsNull(train.trainDiagram.GetCurrentNode());
            Assert.IsTrue(train.IsAutoRun);
            train.Update();
            Assert.IsFalse(train.IsAutoRun);
        }

        [Test]
        public void MoveToNextEntryRaisesCurrentEntryChanged()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var diagram = scenario.Train.trainDiagram;
            diagram.ConsumeCurrentEntryChanged();

            diagram.MoveToNextEntry();

            Assert.IsTrue(diagram.ConsumeCurrentEntryChanged());
            Assert.IsFalse(diagram.ConsumeCurrentEntryChanged());
        }

        [Test]
        public void RemovingCurrentNodeRaisesCurrentEntryChanged()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var diagram = scenario.Train.trainDiagram;
            var currentNode = diagram.GetCurrentNode();
            diagram.ConsumeCurrentEntryChanged();

            diagram.HandleNodeRemoval(currentNode);

            Assert.IsTrue(diagram.ConsumeCurrentEntryChanged());
            Assert.IsFalse(diagram.ConsumeCurrentEntryChanged());
        }

        [Test]
        public void SaveDataKeepsEntriesAfterReplace()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var diagram = scenario.Train.trainDiagram;
            diagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });

            var saveData = diagram.CreateTrainDiagramSaveData();

            Assert.AreEqual(1, saveData.Entries.Count);
            Assert.AreEqual(0, saveData.CurrentIndex);
            Assert.AreEqual(GameUpdater.TicksPerSecond, saveData.Entries[0].WaitForTicksInitial);
        }
    }
}
