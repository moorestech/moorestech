using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
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
            var env = TrainTestHelper.CreateEnvironment();

            var (stationBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗しました。");
            Assert.IsNotNull(railComponents, "RailComponentの取得に失敗しました。");

            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponentの取得に失敗しました。");

            Assert.IsTrue(stationBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var stationInventory), "駅ブロックのインベントリコンポーネントが見つかりません。");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            stationInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railComponents[0].FrontNode;
            Assert.IsNotNull(entryNode, "駅の入口ノードを取得できませんでした。");
            var exitNode = railComponents[1].FrontNode;
            Assert.IsNotNull(exitNode, "駅の出口ノードを取得できませんでした。");

            var stationSegmentLength = stationBlock!.BlockPositionInfo.BlockSize.z;
            Assert.Greater(stationSegmentLength, 0, "駅セグメントの長さが0以下になっています。");

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, stationSegmentLength, 0);

            var trainCar = TrainTestCarFactory.CreateTrainCar(0, 1000, 1, stationSegmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が駅ブロックにドッキングしていません。");
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "列車貨車のインベントリが初期状態で空になっていません。");
            // 伸長60tick + 接触1tickで一括転送
            // Transfer in bulk after 60 ticks extend + 1 tick contact
            const int transferTicks = 61;
            for (var i = 0; i < transferTicks; i++)
            {
                stationComponent.Update();
            }

            var remainingStack = stationInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "駅インベントリのスロットが移送後も空になっていません。");

            var carStack = trainCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Id, "列車貨車が駅のアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, carStack.Count, "列車貨車が駅インベントリの全量を受け取っていません。");

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void CargoPlatformTransfersItemsToDockedTrainCar()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (cargoPlatformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoPlatformBlock, "貨物プラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(railComponents, "RailComponentの取得に失敗しました。");

            var cargoPlatformComponent = cargoPlatformBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformComponent, "CargoplatformComponentの取得に失敗しました。");

            Assert.IsTrue(cargoPlatformBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var cargoInventory), "貨物プラットフォームのインベントリコンポーネントが見つかりません。");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            cargoInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railComponents[0].FrontNode;
            Assert.IsNotNull(entryNode, "貨物プラットフォームの入口ノードを取得できませんでした。");
            var exitNode = railComponents[1].FrontNode;
            Assert.IsNotNull(exitNode, "貨物プラットフォームの出口ノードを取得できませんでした。");

            var platformSegmentLength = cargoPlatformBlock!.BlockPositionInfo.BlockSize.z;
            Assert.Greater(platformSegmentLength, 0, "貨物プラットフォームセグメントの長さが0以下になっています。");

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, platformSegmentLength, 0);

            var trainCar = TrainTestCarFactory.CreateTrainCar(0, 1000, 1, platformSegmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "列車貨車のインベントリが初期状態で空になっていません。");

            // 伸長60tick + 接触1tickで一括転送
            // 伸長60tick + 接触1tickで一括転送
            // Transfer in bulk after 60 ticks extend + 1 tick contact
            const int transferTicks = 61;
            for (var i = 0; i < transferTicks; i++)
            {
                cargoPlatformComponent.Update();
            }

            var remainingStack = cargoInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "貨物プラットフォームのインベントリスロットが移送後も空になっていません。");

            var carStack = trainCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Id, "列車貨車が貨物プラットフォームのアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, carStack.Count, "列車貨車が貨物プラットフォームから全量を受け取っていません。");

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void CargoPlatformReceivesItemsFromTrainCarWhenInUnloadMode()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (cargoPlatformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoPlatformBlock, "貨物プラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(railComponents, "RailComponentの取得に失敗しました。");

            var cargoPlatformComponent = cargoPlatformBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformComponent, "CargoplatformComponentの取得に失敗しました。");

            Assert.IsTrue(cargoPlatformBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var cargoInventory),
                "貨物プラットフォームのインベントリコンポーネントが見つかりません。");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            cargoInventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            var entryNode = railComponents[0].FrontNode;
            Assert.IsNotNull(entryNode, "貨物プラットフォームの入口ノードを取得できませんでした。");
            var exitNode = railComponents[1].FrontNode;
            Assert.IsNotNull(exitNode, "貨物プラットフォームの出口ノードを取得できませんでした。");

            var platformSegmentLength = cargoPlatformBlock!.BlockPositionInfo.BlockSize.z;
            Assert.Greater(platformSegmentLength, 0, "貨物プラットフォームセグメントの長さが0以下になっています。");

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, platformSegmentLength, 0);

            var trainCar = TrainTestCarFactory.CreateTrainCar(0, 1000, 1, platformSegmentLength, true);
            trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            cargoPlatformComponent.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");

                        // 伸長60tick + 接触1tickで一括転送
            // 伸長60tick + 接触1tickで一括転送
            // Transfer in bulk after 60 ticks extend + 1 tick contact
            const int transferTicks = 61;
            for (var i = 0; i < transferTicks; i++)
            {
                cargoPlatformComponent.Update();
            }

            var cargoStack = cargoInventory.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, cargoStack.Id, "貨物プラットフォームが荷降ろしされたアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, cargoStack.Count, "貨物プラットフォームが列車の積荷を全量受け取っていません。");

            var remainingCarStack = trainCar.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingCarStack.Id, "荷降ろし後も列車貨車のインベントリが空になっていません。");

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void StationRejectsSecondTrainWhileFirstRemainsDocked()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (stationBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗しました。");
            Assert.IsNotNull(railComponents, "RailComponentの取得に失敗しました。");
            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponentの取得に失敗しました。");

            Assert.IsTrue(stationBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var stationInventory),
                "駅ブロックのインベントリコンポーネントが見つかりません。");

            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;

            Assert.IsNotNull(entryNode, "駅の入口ノードを取得できませんでした。");
            Assert.IsNotNull(exitNode, "駅の出口ノードを取得できませんでした。");

            var stationSegmentLength = stationBlock!.BlockPositionInfo.BlockSize.z;
            Assert.Greater(stationSegmentLength, 0, "駅セグメントの長さが0以下になっています。");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            stationInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            TrainUnit CreateTrain(out TrainCar car)
            {
                var railNodes = new List<IRailNode> { exitNode, entryNode };
                var railPosition = new RailPosition(railNodes, stationSegmentLength, 0);
                car = TrainTestCarFactory.CreateTrainCar(0, 1000, 1, stationSegmentLength, true);
                return new TrainUnit(railPosition, new List<TrainCar> { car }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            }

            var firstTrain = CreateTrain(out var firstCar);
            firstTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(firstCar.IsDocked, "1列車目が駅にドッキングできていません。");

            var secondTrain = CreateTrain(out var secondCar);
            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsFalse(secondCar.IsDocked, "駅占有中にも関わらず2列車目がドッキングしています。");

            // 伸長60tick + 接触1tickで一括転送
            // Transfer in bulk after 60 ticks extend + 1 tick contact
            const int transferTicks = 61;
            for (var i = 0; i < transferTicks; i++)
            {
                stationComponent.Update();
            }

            var remainingStack = stationInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "駅インベントリがドッキング中の列車へ全てのアイテムを移送できていません。");

            var firstCarStack = firstCar.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, firstCarStack.Id, "1列車目が駅のアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, firstCarStack.Count, "1列車目が駅インベントリの全量を受け取っていません。");

            var secondCarStack = secondCar.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, secondCarStack.Id, "2列車目のインベントリが空のまま維持されていません。");

            firstTrain.trainUnitStationDocking.UndockFromStation();
            secondTrain.trainUnitStationDocking.UndockFromStation();

            env.GetTrainDiagramManager().UnregisterDiagram(firstTrain.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(firstTrain);
            env.GetTrainDiagramManager().UnregisterDiagram(secondTrain.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(secondTrain);
        }

    }
}

