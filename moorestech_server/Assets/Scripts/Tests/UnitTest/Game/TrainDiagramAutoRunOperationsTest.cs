using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Util;
using Game.Context;
using Tests.Module.TestMod;

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
            var currentDestination = trainUnit.trainDiagram.GetCurrentNode();

            Assert.IsNotNull(currentDestination, "Scenario must start with an active destination.");
            Assert.Greater(trainUnit.trainDiagram.Entries.Count, 1,
                "Scenario should provide a non-current entry to remove.");

            var dockingStateBefore = trainUnit.trainUnitStationDocking.IsDocked;
            var autoRunBefore = trainUnit.IsAutoRun;

            var nonCurrentNode = trainUnit.trainDiagram.Entries
                .Select(entry => entry.Node)
                .Last(node => node != currentDestination);

            trainUnit.trainDiagram.HandleNodeRemoval(nonCurrentNode);

            Assert.AreEqual(autoRunBefore, trainUnit.IsAutoRun,
                "Removing a non-current entry should not toggle auto-run.");
            Assert.AreEqual(dockingStateBefore, trainUnit.trainUnitStationDocking.IsDocked,
                "Docking state must remain unchanged when a non-current entry is removed.");
            Assert.AreSame(currentDestination, trainUnit.trainDiagram.GetCurrentNode(),
                "Current destination should be preserved after removing a non-current entry.");

            trainUnit.Update();

            Assert.AreEqual(autoRunBefore, trainUnit.IsAutoRun,
                "Auto-run should remain stable after subsequent updates.");
            Assert.AreEqual(dockingStateBefore, trainUnit.trainUnitStationDocking.IsDocked,
                "Docking state should remain stable after subsequent updates.");
            Assert.AreSame(currentDestination, trainUnit.trainDiagram.GetCurrentNode(),
                "Train should continue heading towards the same destination after removing a non-current entry.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation2_RemovingCurrentEntryAdvancesAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;

            var diagram = trainUnit.trainDiagram;
            var currentDestination = diagram.GetCurrentNode();

            Assert.IsNotNull(currentDestination, "Scenario must start with an active destination.");
            Assert.Greater(diagram.Entries.Count, 1,
                "Scenario should provide a subsequent entry to advance towards.");

            var nextIndex = (diagram.CurrentIndex + 1) % diagram.Entries.Count;
            var nextNode = diagram.Entries[nextIndex].Node;

            diagram.HandleNodeRemoval(currentDestination);

            Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain enabled after removing the active entry.");
            Assert.AreSame(nextNode, diagram.GetCurrentNode(),
                "Current diagram entry should advance to the next node after removal.");

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "Train should remain docked until the next tick in the docked scenario.");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Train should undock on the next tick after removing the active entry.");
                Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain enabled after the update.");
                Assert.AreSame(nextNode, diagram.GetCurrentNode(),
                    "Train should now be heading towards the next node after undocking.");
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Running scenario should remain undocked after removing the active entry.");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Running scenario should stay undocked on subsequent ticks.");
                Assert.IsTrue(trainUnit.IsAutoRun, "Auto-run should remain enabled while running.");
                Assert.AreSame(nextNode, diagram.GetCurrentNode(),
                    "Running scenario should continue towards the advanced node after updates.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation3_RemovingAllEntriesStopsAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            var nodesToRemove = diagram.Entries
                .Select(entry => entry.Node)
                .Distinct()
                .ToList();

            Assert.IsNotEmpty(nodesToRemove,
                "Scenario must provide entries to remove from the diagram.");

            foreach (var node in nodesToRemove)
            {
                diagram.HandleNodeRemoval(node);
            }

            Assert.IsEmpty(diagram.Entries,
                "Diagram should have no entries after removing every node.");

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "Docked scenario should still report the train as docked immediately after removal.");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Removing all entries should undock the train on the next tick.");
                Assert.IsTrue(trainUnit.IsAutoRun,
                    "Auto-run remains active until the train confirms there are no destinations left.");

                trainUnit.Update();
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Running scenario should begin in an undocked state.");

                trainUnit.Update();
            }

            Assert.IsFalse(trainUnit.IsAutoRun,
                "Removing all diagram entries must disable auto-run once processed.");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                "Train should remain undocked after auto-run is disabled.");
            Assert.IsNull(diagram.GetCurrentNode(),
                "Diagram without entries should not report a next destination.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation4_RemovingCurrentEntrySkipsDisconnectedNext(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            Assert.GreaterOrEqual(diagram.Entries.Count, 3,
                "Scenario must provide at least three entries (start, n1, n2).");

            var startNode = diagram.Entries[0].Node;
            var firstDestination = diagram.Entries[1].Node;
            var secondDestination = diagram.Entries[2].Node;

            Assert.AreSame(startNode, diagram.GetCurrentNode(),
                "Initial destination should be the station exit node.");

            startNode.DisconnectNode(firstDestination);
            firstDestination.DisconnectNode(startNode);

            Assert.IsFalse(startNode.ConnectedNodes.Contains(firstDestination),
                "Start node should no longer connect directly to the first destination.");

            const int rerouteDistance = 4321;
            startNode.ConnectNode(secondDestination, rerouteDistance);
            secondDestination.ConnectNode(startNode, rerouteDistance);

            diagram.HandleNodeRemoval(startNode);

            Assert.IsTrue(trainUnit.IsAutoRun,
                "Removing the current entry should leave auto-run enabled while a route remains.");

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "Docked scenario should still be docked immediately after removal.");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Train should undock after the current entry is removed.");
                Assert.AreSame(secondDestination, diagram.GetCurrentNode(),
                    "Docked scenario should advance to the reachable second destination after undocking.");
                Assert.IsTrue(trainUnit.IsAutoRun,
                    "Auto-run should remain enabled after undocking.");
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Running scenario should begin undocked.");

                const int maxUpdates = 8;
                for (var i = 0; i < maxUpdates; i++)
                {
                    trainUnit.Update();
                    if (ReferenceEquals(diagram.GetCurrentNode(), secondDestination))
                    {
                        break;
                    }
                }

                Assert.AreSame(secondDestination, diagram.GetCurrentNode(),
                    "Running scenario should reroute to the reachable second destination.");
                Assert.IsTrue(trainUnit.IsAutoRun,
                    "Auto-run should remain enabled while rerouting to a reachable node.");
            }

            trainUnit.Update();

            Assert.AreSame(secondDestination, diagram.GetCurrentNode(),
                "Train should continue targeting the reachable destination on subsequent updates.");
            Assert.IsTrue(trainUnit.IsAutoRun,
                "Auto-run should remain enabled after the reroute stabilises.");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                "Train should be travelling rather than docked after rerouting.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation5_RemovingCurrentEntryStopsAutoRunWhenNoPathsRemain(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            Assert.GreaterOrEqual(diagram.Entries.Count, 4,
                "Scenario must provide start, n1, n2, and n0 entries for operation 5.");

            var startNode = diagram.Entries[0].Node;
            var firstNode = diagram.Entries[1].Node;
            var secondNode = diagram.Entries[2].Node;
            var fallbackNode = diagram.Entries[3].Node;

            startNode.DisconnectNode(firstNode);
            firstNode.DisconnectNode(startNode);
            firstNode.DisconnectNode(secondNode);
            secondNode.DisconnectNode(firstNode);

            diagram.HandleNodeRemoval(startNode);

            Assert.IsTrue(diagram.Entries.Any(entry => ReferenceEquals(entry.Node, fallbackNode)),
                "Fallback node entry should remain after removing the start node.");

            var autoRunDisabled = false;
            var undockObserved = trainUnit.trainUnitStationDocking.IsDocked == false;
            const int maxUpdates = 48;

            for (var i = 0; i < maxUpdates; i++)
            {
                trainUnit.Update();
                undockObserved |= !trainUnit.trainUnitStationDocking.IsDocked;
                if (!trainUnit.IsAutoRun)
                {
                    autoRunDisabled = true;
                    break;
                }
            }

            Assert.IsTrue(undockObserved,
                "Train should undock when the current entry becomes unreachable.");
            Assert.IsTrue(autoRunDisabled,
                "Auto-run must eventually disable when no reachable entries remain.");
            Assert.IsNull(diagram.GetCurrentNode(),
                "Diagram with no reachable entries should not expose a next destination.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation6_InsertingEntryMaintainsCycleOrder(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            Assert.GreaterOrEqual(diagram.Entries.Count, 4,
                "Scenario must provide start, n1, n2, and n0 entries for operation 6.");

            var startNode = diagram.Entries[0].Node;
            var n1 = diagram.Entries[1].Node;
            var n2 = diagram.Entries[2].Node;
            var n0 = diagram.Entries[3].Node;

            ConnectNodePair(n2, n0, 7777);

            diagram.HandleNodeRemoval(n2);

            Assert.AreEqual(3, diagram.Entries.Count,
                "Diagram should contain start, n1, and n0 after removing n2.");
            Assert.AreSame(startNode, diagram.Entries[0].Node,
                "Start node must remain the current entry prior to insertion.");
            Assert.AreSame(n1, diagram.Entries[1].Node,
                "Second entry should be n1 prior to insertion.");
            Assert.AreSame(n0, diagram.Entries[2].Node,
                "Third entry should be n0 prior to insertion.");

            diagram.InsertEntry(2, n2);

            CollectionAssert.AreEqual(
                new[] { startNode, n1, n2, n0 },
                diagram.Entries.Select(entry => entry.Node).ToArray(),
                "Inserted node should produce start -> n1 -> n2 -> n0 order.");

            var startEntry = diagram.Entries[0];
            if (!startRunning)
            {
                startEntry.SetDepartureWaitTicks(0);
            }

            var visitedDestinations = new List<RailNode> { diagram.GetCurrentNode() };
            const int maxUpdates = 256;

            for (var i = 0; i < maxUpdates; i++)
            {
                trainUnit.Update();
                var currentDestination = diagram.GetCurrentNode();
                if (!ReferenceEquals(currentDestination, visitedDestinations.Last()))
                {
                    visitedDestinations.Add(currentDestination);
                    if (ReferenceEquals(currentDestination, startNode) && visitedDestinations.Count > 1)
                    {
                        break;
                    }
                }
            }

            CollectionAssert.AreEqual(
                new[] { startNode, n1, n2, n0, startNode },
                visitedDestinations,
                "Diagram should cycle through start -> n1 -> n2 -> n0 -> start after insertion.");
            Assert.IsTrue(trainUnit.IsAutoRun,
                "Auto-run should remain active throughout the cycle.");
            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                "Train should be docked after completing the cycle back to the start node.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation7_SingleEntryLoopsDockAndDepart(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            foreach (var entry in diagram.Entries.ToList())
            {
                if (!ReferenceEquals(entry.Node, diagram.Entries[0].Node))
                {
                    diagram.HandleNodeRemoval(entry.Node);
                }
            }

            Assert.AreEqual(1, diagram.Entries.Count,
                "Diagram should contain exactly one entry for the single entry loop test.");

            var startEntry = diagram.Entries[0];
            var trainCar = scenario.TrainCar;

            if (startRunning)
            {
                startEntry.SetDepartureWaitTicks(1);
            }
            else
            {
                startEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);
                var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
                trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));
            }

            var observedDocked = trainUnit.trainUnitStationDocking.IsDocked ? 1 : 0;
            var observedUndocked = trainUnit.trainUnitStationDocking.IsDocked ? 0 : 1;
            var dockingsCompleted = 0;
            var previousDockState = trainUnit.trainUnitStationDocking.IsDocked;

            const int maxUpdates = 256;
            for (var i = 0; i < maxUpdates; i++)
            {
                trainUnit.Update();

                var docked = trainUnit.trainUnitStationDocking.IsDocked;
                if (docked)
                {
                    observedDocked = 1;
                }
                else
                {
                    observedUndocked = 1;
                }

                if (docked && !previousDockState)
                {
                    dockingsCompleted++;
                    if (dockingsCompleted >= 2)
                    {
                        break;
                    }
                }

                previousDockState = docked;
            }

            Assert.AreEqual(1, observedDocked,
                "Loop should include at least one docked state.");
            Assert.AreEqual(1, observedUndocked,
                "Loop should include at least one undocked state.");
            Assert.GreaterOrEqual(dockingsCompleted, 2,
                "Train should repeatedly dock at the single entry station.");
            Assert.IsTrue(trainUnit.IsAutoRun,
                "Auto-run should remain enabled during the single entry loop.");
            Assert.AreSame(diagram.Entries[0].Node, diagram.GetCurrentNode(),
                "Single entry diagram should continually target the lone node.");
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
