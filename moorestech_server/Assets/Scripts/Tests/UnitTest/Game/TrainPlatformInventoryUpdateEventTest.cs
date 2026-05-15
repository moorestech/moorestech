using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
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
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// 駅・アイテムプラットフォームの内部インベントリが変化したとき
    /// BlockOpenableInventoryUpdateEvent が正しく発火するかを検証する
    /// Verify that BlockOpenableInventoryUpdateEvent is raised when the
    /// internal inventory of a station / item platform changes
    /// </summary>
    public class TrainPlatformInventoryUpdateEventTest
    {
        [Test]
        public void CargoPlatformSetItemRaisesInventoryUpdateEvent()
        {
            // 環境とプラットフォームを準備
            // Prepare environment and a cargo item platform
            var env = TrainTestHelper.CreateEnvironment();
            var platformBlock = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainItemPlatform, Vector3Int.zero, BlockDirection.North);
            Assert.IsNotNull(platformBlock, "貨物プラットフォームの設置に失敗しました。");

            var blockInventory = platformBlock.GetComponent<IBlockInventory>();
            var receivedEvents = SubscribeBlockInventoryEvents(platformBlock.BlockInstanceId);

            // インベントリへSetItemし、対応するイベントが発火することを確認
            // SetItem into inventory and confirm the matching update event fires
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 4);
            blockInventory.SetItem(0, stack);

            Assert.AreEqual(1, receivedEvents.Count, "プラットフォームのスロット更新イベントが発火していません。");
            Assert.AreEqual(0, receivedEvents[0].Slot);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, receivedEvents[0].ItemStack.Id);
            Assert.AreEqual(4, receivedEvents[0].ItemStack.Count);
            Assert.AreEqual(platformBlock.BlockInstanceId, receivedEvents[0].BlockInstanceId);
        }

        [Test]
        public void StationSetItemRaisesInventoryUpdateEvent()
        {
            // 駅ブロック側も同じ経路で発火することを確認
            // The same event path must also work for the station block
            var env = TrainTestHelper.CreateEnvironment();
            var stationBlock = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗しました。");

            var blockInventory = stationBlock.GetComponent<IBlockInventory>();
            var receivedEvents = SubscribeBlockInventoryEvents(stationBlock.BlockInstanceId);

            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3);
            blockInventory.SetItem(2, stack);

            Assert.AreEqual(1, receivedEvents.Count, "駅ブロックのスロット更新イベントが発火していません。");
            Assert.AreEqual(2, receivedEvents[0].Slot);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, receivedEvents[0].ItemStack.Id);
            Assert.AreEqual(3, receivedEvents[0].ItemStack.Count);
            Assert.AreEqual(stationBlock.BlockInstanceId, receivedEvents[0].BlockInstanceId);
        }

        [Test]
        public void CargoPlatformInsertItemRaisesInventoryUpdateEvent()
        {
            // InsertItem経由でも発火する（ベルト等からの挿入経路）
            // InsertItem path (used by belts etc.) must also raise the event
            var env = TrainTestHelper.CreateEnvironment();
            var platformBlock = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainItemPlatform, Vector3Int.zero, BlockDirection.North);

            var blockInventory = platformBlock.GetComponent<IBlockInventory>();
            var receivedEvents = SubscribeBlockInventoryEvents(platformBlock.BlockInstanceId);

            var inserted = blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 7), InsertItemContext.Empty);

            Assert.AreEqual(0, inserted.Count, "プラットフォームの空スロットへ挿入できていません。");
            Assert.AreEqual(1, receivedEvents.Count, "InsertItem経由でイベントが発火していません。");
            Assert.AreEqual(ForUnitTestItemId.ItemId1, receivedEvents[0].ItemStack.Id);
            Assert.AreEqual(7, receivedEvents[0].ItemStack.Count);
        }

        [Test]
        public void LoadingToTrainEmitsClearingEventsForEmptiedSlots()
        {
            // 駅 → 列車への一括積み込み時、駅側のスロットが空になることをイベントで通知する
            // When the platform hands its container off to the docked train, slots must be reported as empty
            var env = TrainTestHelper.CreateEnvironment();
            var (cargoPlatformBlock, railComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainItemPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            var trainPlatformItemTransferComponent = cargoPlatformBlock.GetComponent<TrainPlatformItemContainerComponent>();
            var trainPlatformDockingComponent = cargoPlatformBlock.GetComponent<TrainPlatformDockingComponent>();
            var cargoInventory = cargoPlatformBlock.GetComponent<IBlockInventory>();

            // 事前に積んでおく（SetItem時点で1回イベントが出るが、ここでは別の検証）
            // Pre-load one slot (this raises one event but we re-create the receiver right after)
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            cargoInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            var entryNode = railComponents[0].FrontNode;
            var exitNode = railComponents[1].FrontNode;
            var platformSegmentLength = cargoPlatformBlock.BlockPositionInfo.BlockSize.z;

            var railNodes = new List<IRailNode> { exitNode, entryNode };
            var railPosition = new RailPosition(railNodes, platformSegmentLength, 0);

            var (trainCar, _) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, platformSegmentLength, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");

            // ここから新しい受信者でイベントを記録する
            // Now start recording events with a fresh receiver
            var receivedEvents = SubscribeBlockInventoryEvents(cargoPlatformBlock.BlockInstanceId);

            // 転送完了までUpdateを回す
            // Run Update until the transfer completes
            var transferTicks = GetCargoTransferTicks();
            for (var i = 0; i < transferTicks; i++)
            {
                trainPlatformItemTransferComponent.Update();
                trainPlatformDockingComponent.Update();
            }

            Assert.AreEqual(ItemMaster.EmptyItemId, cargoInventory.GetItem(0).Id, "積み込み後にプラットフォームのスロットが空になっていません。");

            // 0番スロットが空になったことを通知するイベントが少なくとも1件あること
            // At least one event must report slot 0 becoming empty
            var clearingEvent = receivedEvents.Find(e => e.Slot == 0 && e.ItemStack.Id == ItemMaster.EmptyItemId);
            Assert.IsNotNull(clearingEvent, "積み込み後の空化を通知するイベントが発火していません。");
        }

        private static List<BlockOpenableInventoryUpdateEventProperties> SubscribeBlockInventoryEvents(BlockInstanceId targetInstanceId)
        {
            var captured = new List<BlockOpenableInventoryUpdateEventProperties>();
            ServerContext.BlockOpenableInventoryUpdateEvent.OnInventoryUpdated
                .Where(properties => properties.BlockInstanceId == targetInstanceId)
                .Subscribe(captured.Add);
            return captured;
        }

        private static int GetCargoTransferTicks()
        {
            var cargoParam = (TrainItemPlatformBlockParam)
                MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainItemPlatform).BlockParam;
            var ticks = GameUpdater.SecondsToTicks(cargoParam.LoadingAnimeSpeed);
            return (ticks > int.MaxValue ? int.MaxValue : (int)ticks) + 1;
        }
    }
}
