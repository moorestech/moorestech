using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.Train.Event;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class TrainCarInventoryTest
    {
        [Test]
        public void EnumerateInventory_ReturnsAllSlotStacks()
        {
            TrainTestHelper.CreateEnvironment();

            var (_, container) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 3, 10, true);

            var filledStack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2);
            container.SetItem(1, filledStack);

            var inventorySnapshot = container.InventoryItems;

            Assert.AreEqual(3, inventorySnapshot.Count, "Expected InventoryItems to return all inventory slots.");
            Assert.AreEqual(ItemMaster.EmptyItemId, inventorySnapshot[0].Id, "First slot should remain empty.");
            Assert.AreEqual(filledStack, inventorySnapshot[1], "Slot stack should match the assigned stack.");
            Assert.AreEqual(ItemMaster.EmptyItemId, inventorySnapshot[2].Id, "Third slot should remain empty.");
        }

        [Test]
        public void MergeFrom_FillsExistingStackAndPlacesRemainderNearMatchingSlot()
        {
            TrainTestHelper.CreateEnvironment();

            var (_, target) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 4, 10, true);
            var (_, source) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 4, 10, true);

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;

            // target は index 2 だけ既存スタック。サービス委譲の挙動では余りは同種スタックの近傍に流れる
            // Only index 2 holds an existing stack; service-driven overflow spreads to nearby slots first.
            target.SetItem(2, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack - 1));
            source.SetItem(2, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 5));

            target.MergeFrom(source);

            // 同種スタックがmaxまで埋まり、余りはoriginSlot=2の近傍順(1→3→0)で空スロットへ
            // The same-id stack fills to max, and the remainder follows proximity order around origin slot 2 (1→3→0).
            Assert.AreEqual(maxStack, target.InventoryItems[2].Count, "既存同種スタックがmaxまで埋まっていません。");
            Assert.AreEqual(ForUnitTestItemId.ItemId1, target.InventoryItems[1].Id, "余りが近傍スロット(index 1)へ流れていません。");
            Assert.AreEqual(4, target.InventoryItems[1].Count, "余り個数が想定と一致しません。");
            Assert.AreEqual(ItemMaster.EmptyItemId, target.InventoryItems[0].Id, "近傍より遠いスロットには波及してはならない。");
            Assert.AreEqual(ItemMaster.EmptyItemId, target.InventoryItems[3].Id, "近傍より遠いスロットには波及してはならない。");
            Assert.AreEqual(ItemMaster.EmptyItemId, source.InventoryItems[2].Id, "ソース側の元スロットが空に戻っていません。");
        }

        [Test]
        public void MergeFrom_EmitsInventoryUpdateForBothCarsViaTrainUpdateEvent()
        {
            TrainTestHelper.CreateEnvironment();

            var (targetCar, target) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 2, 10, true);
            var (sourceCar, source) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 2, 10, true);

            // 事前のSetItemも通知に乗るため、購読開始は転送直前に行う
            // SetItem before merging also notifies, so subscribe right before the transfer.
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));

            var trainUpdateEvent = ServerContext.GetService<ITrainUpdateEvent>();
            var events = new List<TrainInventoryUpdateEventProperties>();
            using var subscription = trainUpdateEvent.OnInventoryUpdated.Subscribe(events.Add);

            target.MergeFrom(source);

            // 宛先カー（targetCar）への挿入通知が現在のTrainCarInstanceIdで届くこと
            // The target car receives an update bound to its current TrainCarInstanceId.
            Assert.IsTrue(events.Exists(e =>
                    e.TrainCarInstanceId == targetCar.TrainCarInstanceId
                    && e.ItemStack.Id == ForUnitTestItemId.ItemId1
                    && e.ItemStack.Count == 1),
                "targetCarのTrainCarInstanceIdで通知が届いていない。");

            // 送り元カー（sourceCar）にも空化通知が現在のTrainCarInstanceIdで届くこと
            // The source car receives an emptied-slot update bound to its TrainCarInstanceId.
            Assert.IsTrue(events.Exists(e =>
                    e.TrainCarInstanceId == sourceCar.TrainCarInstanceId
                    && e.Slot == 0
                    && e.ItemStack.Id == ItemMaster.EmptyItemId),
                "sourceCarのTrainCarInstanceIdで空化通知が届いていない。");
        }

        [Test]
        public void SetContainer_RebindsNotificationToNewCarsInstanceId()
        {
            TrainTestHelper.CreateEnvironment();

            var (firstCar, container) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 1, 10, true);
            var secondElement = TrainTestCarFactory.CreateMasterElement(0, 0, 1, 10);
            var secondCar = new TrainCar(secondElement, true);

            var trainUpdateEvent = ServerContext.GetService<ITrainUpdateEvent>();
            var events = new List<TrainInventoryUpdateEventProperties>();
            using var subscription = trainUpdateEvent.OnInventoryUpdated.Subscribe(events.Add);

            // detach 後の更新は誰にも通知されない
            // After detach, mutations do not reach the previous car.
            firstCar.SetContainer(null);
            container.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            Assert.IsFalse(events.Exists(e => e.TrainCarInstanceId == firstCar.TrainCarInstanceId),
                "detach後にfirstCar宛のイベントが漏れている。");

            // secondCar へ装着すると、以後の更新は secondCar の TrainCarInstanceId で届く
            // After attaching to secondCar, subsequent updates flow under its TrainCarInstanceId.
            events.Clear();
            secondCar.SetContainer(container);
            container.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2));

            Assert.IsTrue(events.Exists(e =>
                    e.TrainCarInstanceId == secondCar.TrainCarInstanceId
                    && e.ItemStack.Count == 2),
                "secondCarのTrainCarInstanceIdで通知が届いていない。");
            Assert.IsFalse(events.Exists(e => e.TrainCarInstanceId == firstCar.TrainCarInstanceId),
                "再装着後もfirstCar宛の古い通知が混ざっている。");
        }

        [Test]
        public void InventoryChecksReflectEnumeratedStacks()
        {
            TrainTestHelper.CreateEnvironment();

            var (trainCar, itemContainer) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 1, 10, true);

            Assert.IsTrue(trainCar.IsInventoryEmpty(), "New train car inventory should start empty.");
            Assert.IsFalse(trainCar.IsInventoryFull(), "New train car inventory should not be full.");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            var fullStack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack);
            itemContainer.SetItem(0, fullStack);

            Assert.IsTrue(trainCar.IsInventoryFull(), "Inventory should report full after filling the slot to max stack.");
            Assert.IsFalse(trainCar.IsInventoryEmpty(), "Inventory should not report empty when a slot is filled.");
        }
    }
}
