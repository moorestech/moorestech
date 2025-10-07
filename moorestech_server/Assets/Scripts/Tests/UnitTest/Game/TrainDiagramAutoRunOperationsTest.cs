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
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation2_RemovingCurrentEntryAdvancesAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation3_RemovingAllEntriesStopsAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation4_RemovingCurrentEntrySkipsDisconnectedNext(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation5_RemovingCurrentEntryStopsAutoRunWhenNoPathsRemain(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation6_InsertingEntryMaintainsCycleOrder(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation7_SingleEntryLoopsDockAndDepart(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
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
