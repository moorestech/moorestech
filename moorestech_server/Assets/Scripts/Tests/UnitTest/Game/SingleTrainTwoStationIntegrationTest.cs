using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Extension;
using Game.Context;

namespace Tests.UnitTest.Game
{
    public class SingleTrainTwoStationIntegrationTest
    {
        [Test]
        public void TrainCompletesRoundTripBetweenTwoCargoPlatforms()
        {
            _ = new TrainDiagramManager();

            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (loadingBlock, loadingSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);

            var (unloadingBlock, unloadingSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(0, 0, 10),
                BlockDirection.North);

            Assert.IsNotNull(loadingBlock, "Loading platform block placement failed");
            Assert.IsNotNull(loadingSaver, "Loading platform RailSaverComponent is missing");
            Assert.IsNotNull(unloadingBlock, "Unloading platform block placement failed");
            Assert.IsNotNull(unloadingSaver, "Unloading platform RailSaverComponent is missing");

            var loadingEntryComponent = loadingSaver.RailComponents[0];
            var loadingExitComponent = loadingSaver.RailComponents[1];
            var unloadingEntryComponent = unloadingSaver.RailComponents[0];

            var transitRailA = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 3), BlockDirection.North);
            var transitRailB = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 6), BlockDirection.North);

            const int TransitSegmentLength = 2000;
            ConnectFront(loadingExitComponent, transitRailA, TransitSegmentLength);
            ConnectFront(transitRailA, transitRailB, TransitSegmentLength);
            ConnectFront(transitRailB, unloadingEntryComponent, TransitSegmentLength);

            Assert.IsTrue(loadingBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var loadingInventory),
                "Loading platform inventory not found");
            Assert.IsTrue(unloadingBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var unloadingInventory),
                "Unloading platform inventory not found");

            var cargoPlatformLoader = loadingBlock.GetComponent<CargoplatformComponent>();
            var cargoPlatformUnloader = unloadingBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformLoader, "Loading platform component missing");
            Assert.IsNotNull(cargoPlatformUnloader, "Unloading platform component missing");

            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1);
            var maxStack = itemMaster.MaxStack;
            loadingInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));
            unloadingInventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            cargoPlatformLoader.SetTransferMode(CargoplatformComponent.CargoTransferMode.LoadToTrain);
            cargoPlatformUnloader.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);

            var stationSegmentLength = loadingEntryComponent.FrontNode.GetDistanceToNode(loadingExitComponent.FrontNode);
            Assert.Greater(stationSegmentLength, 0, "Station segment length must be positive");

            var initialNodes = new List<RailNode>
            {
                unloadingEntryComponent.FrontNode,
                transitRailB.FrontNode,
                transitRailA.FrontNode,
                loadingExitComponent.FrontNode,
                loadingEntryComponent.FrontNode
            };

            
            foreach (var (current, next) in initialNodes.Zip(initialNodes.Skip(1), (current, next) => (current, next)))
            {
                var segmentLength = next.GetDistanceToNode(current);
                Assert.Greater(segmentLength, 0, "Rail nodes must be connected in reverse traversal order: ");
            }

            var railPosition = new RailPosition(new List<RailNode>(initialNodes), stationSegmentLength, 0);
            var trainCar = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: stationSegmentLength);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainCar.IsDocked, "Train should start docked at the loading platform");
            Assert.AreSame(loadingBlock, trainCar.dockingblock, "Train must dock at the loading platform first");

            var loadingEntry = trainUnit.trainDiagram.AddEntry(loadingEntryComponent.FrontNode);
            loadingEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);

            var unloadingEntry = trainUnit.trainDiagram.AddEntry(unloadingEntryComponent.FrontNode);
            unloadingEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            trainUnit.TurnOnAutoRun();

            AdvanceUntil(trainUnit, () => trainCar.IsInventoryFull(), maxIterations: maxStack * 4,
                "Train inventory did not fill while docked at the loading platform");

            var depletedStack = loadingInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, depletedStack.Id, "Loading platform should transfer all items to the train");

            AdvanceUntil(trainUnit, () => !trainUnit.trainUnitStationDocking.IsDocked, maxIterations: 120,
                "Train failed to depart after filling its cargo");

            AdvanceUntil(trainUnit,
                () => trainCar.IsDocked && ReferenceEquals(trainCar.dockingblock, unloadingBlock),
                maxIterations: 5000,
                "Train did not reach the unloading platform");

            AdvanceUntil(trainUnit, () => trainCar.IsInventoryEmpty(), maxIterations: maxStack * 4,
                "Train inventory did not empty while docked at the unloading platform");

            var receivedStack = unloadingInventory.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, receivedStack.Id, "Unloading platform should receive the transported item");
            Assert.AreEqual(maxStack, receivedStack.Count,
                "Unloading platform should receive the entire stack from the train");

            AdvanceUntil(trainUnit, () => !trainUnit.trainUnitStationDocking.IsDocked, maxIterations: 120,
                "Train failed to depart after unloading its cargo");

            AdvanceUntil(trainUnit,
                () => trainCar.IsDocked && ReferenceEquals(trainCar.dockingblock, loadingBlock),
                maxIterations: 5000,
                "Train did not return to the loading platform to complete the loop");

            trainUnit.trainUnitStationDocking.UndockFromStation();
            TrainDiagramManager.Instance.UnregisterDiagram(trainUnit);
            TrainUpdateService.Instance.UnregisterTrain(trainUnit);
        }

        #region Helpers

        private static void ConnectFront(RailComponent source, RailComponent target, int explicitDistance)
        {
            source.ConnectRailComponent(target, true, true, explicitDistance);
        }

        private static void AdvanceUntil(TrainUnit trainUnit, Func<bool> predicate, int maxIterations, string failureMessage)
        {
            for (var i = 0; i < maxIterations; i++)
            {
                trainUnit.Update();
                if (predicate())
                {
                    return;
                }
            }

            Assert.Fail(failureMessage);
        }

        #endregion
    }
}
