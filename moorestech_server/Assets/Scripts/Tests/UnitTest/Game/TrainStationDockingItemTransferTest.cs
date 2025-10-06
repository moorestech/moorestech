using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
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
    public class TrainStationDockingItemTransferTest
    {
        [Test]
        public void StationTransfersItemsToDockedTrainCar()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (stationBlock, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(stationBlock, "Station block placement failed");
            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponent is missing");

            Assert.IsTrue(stationBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var stationInventory), "Station inventory not found");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            stationInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railSaver.RailComponents[0].FrontNode;
            Assert.IsNotNull(entryNode, "Entry node not found");
            var exitNode = railSaver.RailComponents[1].FrontNode;
            Assert.IsNotNull(exitNode, "Exit node not found");

            var stationSegmentLength = entryNode!.GetDistanceToNode(exitNode!);
            Assert.Greater(stationSegmentLength, 0, "Station segment length must be positive");

            var railNodes = new List<RailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, stationSegmentLength, 0);

            var trainCar = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: stationSegmentLength);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "Train car should be docked to the station block");
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "Train car inventory should start empty");

            for (var i = 0; i < maxStack; i++)
            {
                trainUnit.trainUnitStationDocking.TickDockedStations();
            }

            var remainingStack = stationInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "Station inventory slot should be empty after transfer");

            var carStack = trainCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Id, "Train car should receive the station item");
            Assert.AreEqual(maxStack, carStack.Count, "Train car should receive all items from the station slot");

            TrainDiagramManager.Instance.UnregisterDiagram(trainUnit);
            TrainUpdateService.Instance.UnregisterTrain(trainUnit);
        }

        [Test]
        public void CargoPlatformTransfersItemsToDockedTrainCar()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (cargoPlatformBlock, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoPlatformBlock, "Cargo platform block placement failed");
            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

            var cargoPlatformComponent = cargoPlatformBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformComponent, "CargoplatformComponent is missing");

            Assert.IsTrue(cargoPlatformBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var cargoInventory), "Cargo platform inventory not found");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            cargoInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railSaver.RailComponents[0].FrontNode;
            Assert.IsNotNull(entryNode, "Entry node not found");
            var exitNode = railSaver.RailComponents[1].FrontNode;
            Assert.IsNotNull(exitNode, "Exit node not found");

            var platformSegmentLength = entryNode!.GetDistanceToNode(exitNode!);
            Assert.Greater(platformSegmentLength, 0, "Cargo platform segment length must be positive");

            var railNodes = new List<RailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, platformSegmentLength, 0);

            var trainCar = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: platformSegmentLength);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "Train car should be docked to the cargo platform block");
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "Train car inventory should start empty");

            for (var i = 0; i < maxStack; i++)
            {
                trainUnit.trainUnitStationDocking.TickDockedStations();
            }

            var remainingStack = cargoInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "Cargo platform inventory slot should be empty after transfer");

            var carStack = trainCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Id, "Train car should receive the cargo platform item");
            Assert.AreEqual(maxStack, carStack.Count, "Train car should receive all items from the cargo platform slot");

            TrainDiagramManager.Instance.UnregisterDiagram(trainUnit);
            TrainUpdateService.Instance.UnregisterTrain(trainUnit);
        }

        [Test]
        public void CargoPlatformReceivesItemsFromTrainCarWhenInUnloadMode()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (cargoPlatformBlock, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoPlatformBlock, "Cargo platform block placement failed");
            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

            var cargoPlatformComponent = cargoPlatformBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformComponent, "CargoplatformComponent is missing");

            Assert.IsTrue(cargoPlatformBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var cargoInventory),
                "Cargo platform inventory not found");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            cargoInventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            var entryNode = railSaver.RailComponents[0].FrontNode;
            Assert.IsNotNull(entryNode, "Entry node not found");
            var exitNode = railSaver.RailComponents[1].FrontNode;
            Assert.IsNotNull(exitNode, "Exit node not found");

            var platformSegmentLength = entryNode!.GetDistanceToNode(exitNode!);
            Assert.Greater(platformSegmentLength, 0, "Cargo platform segment length must be positive");

            var railNodes = new List<RailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, platformSegmentLength, 0);

            var trainCar = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: platformSegmentLength);
            trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            cargoPlatformComponent.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "Train car should be docked to the cargo platform block");

            for (var i = 0; i < maxStack; i++)
            {
                trainUnit.trainUnitStationDocking.TickDockedStations();
            }

            var cargoStack = cargoInventory.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, cargoStack.Id, "Cargo platform should receive the unloaded item");
            Assert.AreEqual(maxStack, cargoStack.Count, "Cargo platform should receive the entire train stack");

            var remainingCarStack = trainCar.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingCarStack.Id, "Train car inventory should be empty after unloading");

            TrainDiagramManager.Instance.UnregisterDiagram(trainUnit);
            TrainUpdateService.Instance.UnregisterTrain(trainUnit);
        }

        [Test]
        public void StationRejectsSecondTrainWhileFirstRemainsDocked()
        {
            var env = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (stationBlock, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(stationBlock, "Station block placement failed");
            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

            Assert.IsTrue(stationBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var stationInventory),
                "Station inventory not found");

            var entryNode = railSaver.RailComponents[0].FrontNode;
            var exitNode = railSaver.RailComponents[1].FrontNode;

            Assert.IsNotNull(entryNode, "Entry node not found");
            Assert.IsNotNull(exitNode, "Exit node not found");

            var stationSegmentLength = entryNode!.GetDistanceToNode(exitNode!);
            Assert.Greater(stationSegmentLength, 0, "Station segment length must be positive");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            stationInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            TrainUnit CreateTrain(out TrainCar car)
            {
                var railNodes = new List<RailNode> { exitNode, entryNode };
                var railPosition = new RailPosition(railNodes, stationSegmentLength, 0);
                car = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: stationSegmentLength);
                return new TrainUnit(railPosition, new List<TrainCar> { car });
            }

            var firstTrain = CreateTrain(out var firstCar);
            firstTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(firstCar.IsDocked, "First train should dock successfully");

            var secondTrain = CreateTrain(out var secondCar);
            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsFalse(secondCar.IsDocked, "Second train must remain undocked while station is occupied");

            for (var i = 0; i < maxStack; i++)
            {
                firstTrain.trainUnitStationDocking.TickDockedStations();
                secondTrain.trainUnitStationDocking.TickDockedStations();
            }

            var remainingStack = stationInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "Station inventory should transfer items to the docked train");

            var firstCarStack = firstCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, firstCarStack.Id, "First train should receive station items");
            Assert.AreEqual(maxStack, firstCarStack.Count, "First train should receive the full stack");

            var secondCarStack = secondCar.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, secondCarStack.Id, "Second train inventory must remain empty");

            firstTrain.trainUnitStationDocking.UndockFromStation();
            secondTrain.trainUnitStationDocking.UndockFromStation();

            TrainDiagramManager.Instance.UnregisterDiagram(firstTrain);
            TrainUpdateService.Instance.UnregisterTrain(firstTrain);
            TrainDiagramManager.Instance.UnregisterDiagram(secondTrain);
            TrainUpdateService.Instance.UnregisterTrain(secondTrain);
        }

    }
}

