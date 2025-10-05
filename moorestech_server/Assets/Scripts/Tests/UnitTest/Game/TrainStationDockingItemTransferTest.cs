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

    }
}

