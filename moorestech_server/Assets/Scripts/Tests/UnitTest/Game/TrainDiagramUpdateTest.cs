using System.Collections.Generic;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramUpdateTest
    {
        [Test]
        public void DiagramRemovesDeletedNodeAndResetsIndex()
        {
            _ = new TrainDiagramManager();

            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            var startNode = new RailNode();
            var removedNode = new RailNode();
            var nextNode = new RailNode();

            startNode.ConnectNode(removedNode, 10);
            removedNode.ConnectNode(startNode, 10);
            removedNode.ConnectNode(nextNode, 15);
            nextNode.ConnectNode(removedNode, 15);

            var cars = new List<TrainCar>
            {
                new TrainCar(tractionForce: 1000, inventorySlots: 0, length: 5)
            };

            var railNodes = new List<RailNode> { removedNode, startNode };
            var railPosition = new RailPosition(railNodes, 5, 0);

            var trainUnit = new TrainUnit(railPosition, cars);
            trainUnit.trainDiagram.AddEntry(removedNode);
            trainUnit.trainDiagram.AddEntry(nextNode);

            trainUnit.TurnOnAutoRun();
            Assert.IsTrue(trainUnit.IsAutoRun, "Auto run should remain enabled.");

            removedNode.Destroy();

            Assert.IsFalse(trainUnit.trainDiagram._entries.Exists(entry => entry.Node == removedNode), "Removed node should not remain in the diagram.");
            Assert.IsTrue(trainUnit.trainDiagram._entries.Exists(entry => entry.Node == nextNode), "Remaining node should still be present in the diagram.");
            Assert.AreEqual(-1, trainUnit.trainDiagram.currentIndex, "Diagram index should reset after removal.");
            Assert.IsNull(trainUnit.trainDiagram.GetNextDestination(), "Next destination should be cleared.");

            trainUnit.trainDiagram.MoveToNextEntry();

            Assert.AreEqual(nextNode, trainUnit.trainDiagram.GetNextDestination(), "Diagram should advance to the next available node.");
        }
    }
}
