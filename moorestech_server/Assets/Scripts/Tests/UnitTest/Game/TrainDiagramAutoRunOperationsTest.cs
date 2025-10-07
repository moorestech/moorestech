using System.Linq;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramAutoRunOperationsTest
    {
        [SetUp]
        public void SetUp()
        {
            _ = new TrainDiagramManager();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation1_RemovingNonCurrentEntryKeepsAutoRunStable(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;
            var currentDestination = trainUnit.trainDiagram.GetNextDestination();
            Assert.IsNotNull(currentDestination, "Scenario should provide an initial destination.");

            var nonCurrentNode = scenario.DiagramNodes.First(node => node != currentDestination);

            TrainDiagramManager.Instance.NotifyNodeRemoval(nonCurrentNode);

            trainUnit.Update();

            Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain enabled.");
            if (startRunning)
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "Running scenario should remain undocked.");
            }
            else
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "Docked scenario should stay docked.");
            }

            Assert.AreEqual(currentDestination, trainUnit.trainDiagram.GetNextDestination(), "Destination should remain unchanged.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation2_RemovingCurrentEntryAdvancesAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;
            var currentDestination = trainUnit.trainDiagram.GetNextDestination();
            Assert.IsNotNull(currentDestination, "Scenario should provide an initial destination.");

            var entries = trainUnit.trainDiagram.Entries.ToList();
            var currentIndex = entries.FindIndex(entry => entry.Node == currentDestination);
            Assert.GreaterOrEqual(currentIndex, 0, "Current destination must exist within the diagram entries.");

            var expectedNextIndex = (currentIndex + 1) % entries.Count;
            var expectedNextNode = entries[expectedNextIndex].Node;

            TrainDiagramManager.Instance.NotifyNodeRemoval(currentDestination);

            trainUnit.trainDiagram.MoveToNextEntry();

            trainUnit.Update();

            Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain active after advancing to the next entry.");
            Assert.AreEqual(expectedNextNode, trainUnit.trainDiagram.GetNextDestination(), "Train should now target the next entry node.");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "Train should be undocked and heading to the next station.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation3_RemovingAllEntriesStopsAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;

            foreach (var node in scenario.DiagramNodes)
            {
                TrainDiagramManager.Instance.NotifyNodeRemoval(node);
            }

            Assert.AreEqual(0, trainUnit.trainDiagram.Entries.Count, "Diagram should have no entries after clearing.");

            // Allow auto-run logic to process the missing destination.
            trainUnit.Update();
            trainUnit.Update();

            Assert.IsFalse(trainUnit.IsAutoRun, "Auto-run should disable when the diagram is empty.");
            Assert.IsNull(trainUnit.trainDiagram.GetNextDestination(), "There should be no destination after clearing the diagram.");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "Train should not remain docked once auto-run is cleared.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation4_RemovingCurrentEntrySkipsDisconnectedNext(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;
            var entries = trainUnit.trainDiagram.Entries.ToList();
            var currentNode = trainUnit.trainDiagram.GetNextDestination();
            Assert.IsNotNull(currentNode, "Scenario should provide an initial destination.");

            var currentIndex = entries.FindIndex(entry => entry.Node == currentNode);
            var nextNode = entries[(currentIndex + 1) % entries.Count].Node;
            var fallbackNode = entries[(currentIndex + 2) % entries.Count].Node;

            DisconnectNodeFromAllNeighbors(nextNode);
            ConnectNodePair(currentNode, fallbackNode, scenario.StationSegmentLength);

            TrainDiagramManager.Instance.NotifyNodeRemoval(currentNode);
            trainUnit.trainDiagram.MoveToNextEntry();

            // First update undocks (if necessary), second update should discover the fallback path.
            trainUnit.Update();
            trainUnit.Update();

            Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should stay active by targeting the fallback entry.");
            Assert.AreEqual(fallbackNode, trainUnit.trainDiagram.GetNextDestination(), "Train should skip unreachable entry and target fallback.");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "Train should be in transit toward the fallback entry.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation5_RemovingCurrentEntryStopsAutoRunWhenNoPathsRemain(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;
            var entries = trainUnit.trainDiagram.Entries.ToList();
            var currentNode = trainUnit.trainDiagram.GetNextDestination();
            Assert.IsNotNull(currentNode, "Scenario should provide an initial destination.");

            var currentIndex = entries.FindIndex(entry => entry.Node == currentNode);
            var firstCandidate = entries[(currentIndex + 1) % entries.Count].Node;
            var secondCandidate = entries[(currentIndex + 2) % entries.Count].Node;

            DisconnectNodeFromAllNeighbors(firstCandidate);
            DisconnectNodeFromAllNeighbors(secondCandidate);

            TrainDiagramManager.Instance.NotifyNodeRemoval(currentNode);
            trainUnit.trainDiagram.MoveToNextEntry();

            Assert.DoesNotThrow(() => trainUnit.Update(), "Auto-run should handle missing routes without throwing while undocking.");
            Assert.DoesNotThrow(() => trainUnit.Update(), "Auto-run should safely disable after failing to find any routes.");

            Assert.IsFalse(trainUnit.IsAutoRun, "Auto-run should disable when no reachable entries remain.");
            Assert.IsNull(trainUnit.trainDiagram.GetNextDestination(), "No destination should remain when every path is removed.");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "Train should end in an undocked state after auto-run stops.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation6_InsertingEntryMaintainsCycleOrder(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;
            var entries = trainUnit.trainDiagram.Entries.ToList();
            var currentNode = trainUnit.trainDiagram.GetNextDestination();
            Assert.IsNotNull(currentNode, "Scenario should provide an initial destination.");

            var currentIndex = entries.FindIndex(entry => entry.Node == currentNode);
            var nodeB = entries[(currentIndex + 1) % entries.Count].Node;
            var nodeC = entries[(currentIndex + 2) % entries.Count].Node;

            TrainDiagramManager.Instance.NotifyNodeRemoval(nodeB);
            TrainDiagramManager.Instance.NotifyNodeRemoval(nodeC);

            trainUnit.trainDiagram.AddEntry(nodeB);
            trainUnit.trainDiagram.AddEntry(nodeC);

            ConnectNodePair(nodeB, nodeC, scenario.StationSegmentLength);
            ConnectNodePair(nodeC, currentNode, scenario.StationSegmentLength);

            Assert.AreEqual(currentNode, trainUnit.trainDiagram.GetNextDestination(), "Current entry should remain unchanged after reconfiguration.");

            var expectedSequence = new[] { nodeB, nodeC, currentNode };
            foreach (var expected in expectedSequence)
            {
                trainUnit.trainDiagram.MoveToNextEntry();
                Assert.AreEqual(expected, trainUnit.trainDiagram.GetNextDestination(), "Diagram should cycle through the updated entries in order.");
            }

            Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain enabled after updating the diagram sequence.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation7_SingleEntryLoopsDockAndDepart(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);

            var trainUnit = scenario.Train;
            var currentNode = trainUnit.trainDiagram.GetNextDestination();
            Assert.IsNotNull(currentNode, "Scenario should provide an initial destination.");

            foreach (var node in scenario.DiagramNodes)
            {
                if (node != currentNode)
                {
                    TrainDiagramManager.Instance.NotifyNodeRemoval(node);
                }
            }

            Assert.AreEqual(1, trainUnit.trainDiagram.Entries.Count, "Only one diagram entry should remain for the loop test.");

            var remainingEntry = trainUnit.trainDiagram.Entries.First();
            remainingEntry.SetDepartureConditions(null);

            for (var i = 0; i < 3; i++)
            {
                trainUnit.trainUnitStationDocking.TryDockWhenStopped();
                trainUnit.Update();
                trainUnit.trainDiagram.MoveToNextEntry();
                Assert.AreEqual(currentNode, trainUnit.trainDiagram.GetNextDestination(), "Single-entry diagram should always return to the same destination.");
            }

            Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain enabled while looping a single entry.");
        }

        private static TrainAutoRunTestScenario CreateScenario(bool startRunning)
        {
            return startRunning
                ? TrainAutoRunTestScenario.CreateRunningScenario()
                : TrainAutoRunTestScenario.CreateDockedScenario();
        }

        private static void DisconnectNodeFromAllNeighbors(RailNode node)
        {
            if (node == null)
            {
                return;
            }

            foreach (var neighbor in node.ConnectedNodes.ToList())
            {
                node.DisconnectNode(neighbor);
                neighbor.DisconnectNode(node);
            }
        }

        private static void ConnectNodePair(RailNode first, RailNode second, int distance)
        {
            if (first == null || second == null)
            {
                return;
            }

            if (distance <= 0)
            {
                distance = 1;
            }

            first.ConnectNode(second, distance);
            second.ConnectNode(first, distance);
        }
    }
}
