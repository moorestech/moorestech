using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Mooresmaster.Model.BlocksModule;
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
            
            var trainPlatformItemTransferComponent = stationBlock.GetComponent<TrainPlatformItemContainerComponent>();
            var trainPlatformDockingComponent = stationBlock.GetComponent<TrainPlatformDockingComponent>();
            
            Assert.IsNotNull(trainPlatformItemTransferComponent, "trainPlatformItemTransferComponentの取得に失敗しました。");
            Assert.IsNotNull(trainPlatformDockingComponent, "trainPlatformDockingComponentの取得に失敗しました。");

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

            var (trainCar, itemContainer) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, stationSegmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が駅ブロックにドッキングしていません。");
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "列車貨車のインベントリが初期状態で空になっていません。");
            // loadingAnimeSpeed(秒)をtickに変換し、伸長+接触1tick後に一括転送
            // Convert loadingAnimeSpeed seconds to ticks, then transfer after extend + 1 contact tick
            var transferTicks = GetStationTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                trainPlatformDockingComponent.Update();
                trainPlatformItemTransferComponent.Update();
            }

            var remainingStack = stationInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "駅インベントリのスロットが移送後も空になっていません。");
            
            var carStack = itemContainer.InventoryItems[0];
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Stack.Id, "列車貨車が駅のアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, carStack.Stack.Count, "列車貨車が駅インベントリの全量を受け取っていません。");

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void CargoPlatformTransfersItemsToDockedTrainCar()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (cargoPlatformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainItemPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoPlatformBlock, "貨物プラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(railComponents, "RailComponentの取得に失敗しました。");
            
            var trainPlatformItemTransferComponent = cargoPlatformBlock.GetComponent<TrainPlatformItemContainerComponent>();
            var trainPlatformDockingComponent = cargoPlatformBlock.GetComponent<TrainPlatformDockingComponent>();
            
            Assert.IsNotNull(trainPlatformItemTransferComponent, "trainPlatformItemTransferComponentの取得に失敗しました。");
            Assert.IsNotNull(trainPlatformDockingComponent, "trainPlatformDockingComponentの取得に失敗しました。");

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

            var (trainCar, itemContainer) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, platformSegmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");
            Assert.IsTrue(trainCar.IsInventoryEmpty(), "列車貨車のインベントリが初期状態で空になっていません。");

            // loadingAnimeSpeed(秒)をtickに変換し、伸長+接触1tick後に一括転送
            // Convert loadingAnimeSpeed seconds to ticks, then transfer after extend + 1 contact tick
            var transferTicks = GetCargoTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                trainPlatformItemTransferComponent.Update();
                trainPlatformDockingComponent.Update();
            }

            var remainingStack = cargoInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "貨物プラットフォームのインベントリスロットが移送後も空になっていません。");

            var carStack = itemContainer.InventoryItems[0];
            Assert.AreEqual(ForUnitTestItemId.ItemId1, carStack.Stack.Id, "列車貨車が貨物プラットフォームのアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, carStack.Stack.Count, "列車貨車が貨物プラットフォームから全量を受け取っていません。");

            env.GetTrainDiagramManager().UnregisterDiagram(trainUnit.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(trainUnit);
        }

        [Test]
        public void CargoPlatformReceivesItemsFromTrainCarWhenInUnloadMode()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (cargoPlatformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainItemPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(cargoPlatformBlock, "貨物プラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(railComponents, "RailComponentの取得に失敗しました。");
            
            var trainPlatformItemTransferComponentStation = cargoPlatformBlock.GetComponent<TrainPlatformItemContainerComponent>();
            var trainPlatformDockingComponentStation = cargoPlatformBlock.GetComponent<TrainPlatformDockingComponent>();
            var trainPlatformTransferComponentStation = cargoPlatformBlock.GetComponent<TrainPlatformTransferComponent>();
            
            Assert.IsNotNull(trainPlatformItemTransferComponentStation, "trainPlatformItemTransferComponentの取得に失敗しました。");
            Assert.IsNotNull(trainPlatformDockingComponentStation, "trainPlatformDockingComponentの取得に失敗しました。");
            Assert.IsNotNull(trainPlatformTransferComponentStation, "trainPlatformDockingComponentの取得に失敗しました。");

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

            var (trainCar, itemContainer) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, platformSegmentLength, true);
            itemContainer.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainPlatformTransferComponentStation.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");

            // loadingAnimeSpeed(秒)をtickに変換し、伸長+接触1tick後に一括転送
            // Convert loadingAnimeSpeed seconds to ticks, then transfer after extend + 1 contact tick
            var transferTicks = GetCargoTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                trainPlatformItemTransferComponentStation.Update();
                trainPlatformDockingComponentStation.Update();
            }

            var cargoStack = cargoInventory.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, cargoStack.Id, "貨物プラットフォームが荷降ろしされたアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, cargoStack.Count, "貨物プラットフォームが列車の積荷を全量受け取っていません。");

            var remainingCarStack = itemContainer.InventoryItems[0];
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingCarStack.Stack.Id, "荷降ろし後も列車貨車のインベントリが空になっていません。");

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
            
            var trainPlatformItemTransferComponentStation = stationBlock.GetComponent<TrainPlatformItemContainerComponent>();
            var trainPlatformDockingComponentStation = stationBlock.GetComponent<TrainPlatformDockingComponent>();
            var trainPlatformTransferComponentStation = stationBlock.GetComponent<TrainPlatformTransferComponent>();
            
            Assert.IsNotNull(trainPlatformItemTransferComponentStation, "trainPlatformItemTransferComponentの取得に失敗しました。");
            Assert.IsNotNull(trainPlatformDockingComponentStation, "trainPlatformDockingComponentの取得に失敗しました。");
            Assert.IsNotNull(trainPlatformTransferComponentStation, "trainPlatformDockingComponentの取得に失敗しました。");
            
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

            TrainUnit CreateTrain(out TrainCar car, out ItemTrainCarContainer itemContainer)
            {
                var railNodes = new List<IRailNode> { exitNode, entryNode };
                var railPosition = new RailPosition(railNodes, stationSegmentLength, 0);
                car = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, stationSegmentLength, true).trainCar;
                itemContainer = car.Container as ItemTrainCarContainer;
                return new TrainUnit(railPosition, new List<TrainCar> { car }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            }

            var firstTrain = CreateTrain(out var firstCar, out var firstCarContainer);
            firstTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(firstCar.IsDocked, "1列車目が駅にドッキングできていません。");

            var secondTrain = CreateTrain(out var secondCar, out var secondCarContainer);
            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsFalse(secondCar.IsDocked, "駅占有中にも関わらず2列車目がドッキングしています。");

            // loadingAnimeSpeed(秒)をtickに変換し、伸長+接触1tick後に一括転送
            // Convert loadingAnimeSpeed seconds to ticks, then transfer after extend + 1 contact tick
            var transferTicks = GetStationTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                trainPlatformItemTransferComponentStation.Update();
                trainPlatformDockingComponentStation.Update();
            }

            var remainingStack = stationInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainingStack.Id, "駅インベントリがドッキング中の列車へ全てのアイテムを移送できていません。");

            var firstCarStack = firstCarContainer.InventoryItems[0];
            
            
            Assert.AreEqual(ForUnitTestItemId.ItemId1, firstCarStack.Stack.Id, "1列車目が駅のアイテムを受け取っていません。");
            Assert.AreEqual(maxStack, firstCarStack.Stack.Count, "1列車目が駅インベントリの全量を受け取っていません。");

            var secondCarStack = secondCarContainer.InventoryItems[0];
            Assert.AreEqual(ItemMaster.EmptyItemId, secondCarStack.Stack.Id, "2列車目のインベントリが空のまま維持されていません。");

            firstTrain.trainUnitStationDocking.UndockFromStation();
            secondTrain.trainUnitStationDocking.UndockFromStation();

            env.GetTrainDiagramManager().UnregisterDiagram(firstTrain.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(firstTrain);
            env.GetTrainDiagramManager().UnregisterDiagram(secondTrain.trainDiagram);
            env.GetTrainUpdateService().UnregisterTrain(secondTrain);
        }

        private static int GetStationTransferTicks()
        {
            var stationParam = (TrainStationBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;
            return GetTransferTicks(stationParam.LoadingAnimeSpeed) + 1;
        }

        private static int GetCargoTransferTicks()
        {
            var cargoParam = (TrainItemPlatformBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainItemPlatform).BlockParam;
            return GetTransferTicks(cargoParam.LoadingAnimeSpeed) + 1;
        }

        private static int GetTransferTicks(double loadingAnimeSeconds)
        {
            var ticks = GameUpdater.SecondsToTicks(loadingAnimeSeconds);
            return ticks > int.MaxValue ? int.MaxValue : (int)ticks;
        }
    }
}

