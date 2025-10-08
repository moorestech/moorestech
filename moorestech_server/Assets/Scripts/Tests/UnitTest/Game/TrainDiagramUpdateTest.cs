using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

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

            Assert.IsFalse(trainUnit.trainDiagram.Entries.Any(entry => entry.Node == removedNode), "Removed node should not remain in the diagram.");
            Assert.IsTrue(trainUnit.trainDiagram.Entries.Any(entry => entry.Node == nextNode), "Remaining node should still be present in the diagram.");
            Assert.AreEqual(0, trainUnit.trainDiagram.CurrentIndex, "Diagram should promote the next available entry.");
            Assert.AreEqual(nextNode, trainUnit.trainDiagram.GetNextDestination(), "Next destination should advance to the remaining node.");

            trainUnit.trainDiagram.MoveToNextEntry();

            Assert.AreEqual(nextNode, trainUnit.trainDiagram.GetNextDestination(), "Single-entry diagram should remain focused on the remaining node.");
        }

        [Test]
        public void DiagramEntrySupportsInventoryEmptyDepartureCondition()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();

            var trainUnit = scenario.Train;
            var trainCar = scenario.TrainCar;
            trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));

            Assert.IsTrue(trainCar.IsDocked, "Train car should be docked to the cargo platform block");

            var entry = trainUnit.trainDiagram.Entries.First(e => e.Node == scenario.StationExitFront);
            entry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            Assert.IsFalse(entry.CanDepart(trainUnit), "Train should wait until inventory is empty.");

            trainCar.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            Assert.IsTrue(entry.CanDepart(trainUnit), "Train should depart once inventory becomes empty.");
        }

        [Test]
        public void DiagramEntryAllowsManagingMultipleDepartureConditions()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();

            var trainUnit = scenario.Train;
            var trainCar = scenario.TrainCar;
            trainCar.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            Assert.IsTrue(trainCar.IsDocked, "Train car should be docked to the cargo platform block");

            var entry = trainUnit.trainDiagram.Entries.First(e => e.Node == scenario.StationExitFront);
            entry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            Assert.AreEqual(1, entry.DepartureConditions.Count, "Initial departure condition should be set");
            CollectionAssert.AreEqual(
                new[] { TrainDiagram.DepartureConditionType.TrainInventoryEmpty },
                entry.DepartureConditionTypes,
                "Initial departure condition type should be tracked");
            Assert.IsTrue(entry.CanDepart(trainUnit), "Train should be able to depart with empty inventory");

            entry.AddDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);

            Assert.AreEqual(2, entry.DepartureConditions.Count, "Second departure condition should be appended");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    TrainDiagram.DepartureConditionType.TrainInventoryEmpty,
                    TrainDiagram.DepartureConditionType.TrainInventoryFull
                },
                entry.DepartureConditionTypes,
                "Both departure condition types should be tracked");
            Assert.IsFalse(entry.CanDepart(trainUnit), "Conflicting conditions should block departure");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            Assert.IsFalse(entry.CanDepart(trainUnit), "Train should still be blocked while both conditions apply");

            Assert.IsTrue(
                entry.RemoveDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty),
                "Removing the empty condition should succeed");
            Assert.AreEqual(1, entry.DepartureConditions.Count, "Only one condition should remain after removal");
            CollectionAssert.AreEqual(
                new[] { TrainDiagram.DepartureConditionType.TrainInventoryFull },
                entry.DepartureConditionTypes,
                "Remaining condition type should be TrainInventoryFull");

            Assert.IsTrue(entry.CanDepart(trainUnit), "Train should depart once only the full condition remains and is satisfied");

            Assert.IsFalse(
                entry.RemoveDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty),
                "Removing a non-existent condition should return false");

            Assert.IsTrue(
                entry.RemoveDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull),
                "Removing the final condition should succeed");

            Assert.AreEqual(0, entry.DepartureConditions.Count, "All conditions should be cleared after final removal");
            Assert.IsTrue(entry.CanDepart(trainUnit), "No conditions should allow immediate departure");

        }

        [Test]
        public void WaitForTicksDepartureConditionRequiresDockedAutoRunAndResetsAfterDeparture()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();

            var trainUnit = scenario.Train;
            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "Train should start docked at the cargo platform");

            var firstEntry = trainUnit.trainDiagram.Entries.First(entry => entry.Node == scenario.StationExitFront);

            firstEntry.SetDepartureWaitTicks(2);
            CollectionAssert.AreEqual(
                new[] { TrainDiagram.DepartureConditionType.WaitForTicks },
                firstEntry.DepartureConditionTypes,
                "Wait-for-ticks should be the only configured departure condition");

            trainUnit.TurnOnAutoRun();
            Assert.IsTrue(trainUnit.IsAutoRun, "Auto run should be enabled to test wait ticks");

            trainUnit.trainUnitStationDocking.UndockFromStation();
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "Manual undocking should clear docked state");

            Assert.IsFalse(firstEntry.CanDepart(trainUnit), "Undocked trains should not consume wait ticks");

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "Train should be docked before counting down");

            Assert.IsFalse(firstEntry.CanDepart(trainUnit), "First docked tick should decrease remaining time but stay waiting");
            Assert.IsTrue(firstEntry.CanDepart(trainUnit), "Second docked tick should complete the wait");

            trainUnit.trainDiagram.MoveToNextEntry();
            trainUnit.trainDiagram.MoveToNextEntry();

            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "Train remains docked after cycling diagram entries");
            Assert.IsFalse(firstEntry.CanDepart(trainUnit), "Wait ticks should reset after the entry is departed");
        }
    }
}
