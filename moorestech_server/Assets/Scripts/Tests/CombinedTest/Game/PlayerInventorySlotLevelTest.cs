using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class PlayerInventorySlotLevelTest
    {
        // レベル解放でスロット数が上がり、冪等に動作する
        // Unlocking a level raises the slot count and behaves idempotently
        [Test]
        public void UnlockLevelIsIdempotentTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();

            var eventCount = 0;
            store.OnSlotCountChanged.Subscribe(_ => eventCount++);

            Assert.AreEqual(0, store.CurrentLevel);
            Assert.AreEqual(45, store.CurrentSlotCount);

            store.UnlockLevel(1);
            Assert.AreEqual(1, store.CurrentLevel);
            Assert.AreEqual(54, store.CurrentSlotCount);
            Assert.AreEqual(1, eventCount);

            // 同一・下位レベルの再発火では何も起きない
            // Re-firing the same or a lower level changes nothing
            store.UnlockLevel(1);
            store.UnlockLevel(0);
            Assert.AreEqual(1, store.CurrentLevel);
            Assert.AreEqual(1, eventCount);

            // マスタ範囲外は最大レベルにクランプ
            // Levels beyond master definition clamp to the max level
            store.UnlockLevel(99);
            Assert.AreEqual(1, store.CurrentLevel);
        }

        // 拡張しても既存アイテムはスロット位置ごと保持される
        // Expansion preserves existing items at their slot indices
        [Test]
        public void ExpandSlotsKeepsExistingItemsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var updatedSlots = new List<int>();
            var service = new OpenableInventoryItemDataStoreService((slot, _) => updatedSlots.Add(slot), ServerContext.ItemStackFactory, 45);
            service.SetItem(0, new ItemId(1), 5);
            service.SetItem(44, new ItemId(2), 7);
            updatedSlots.Clear();

            service.ExpandSlots(54);

            Assert.AreEqual(54, service.GetSlotSize());
            Assert.AreEqual(5, service.GetItem(0).Count);
            Assert.AreEqual(7, service.GetItem(44).Count);
            Assert.AreEqual(new[] { 45, 46, 47, 48, 49, 50, 51, 52, 53 }, updatedSlots);

            // 縮小要求は無視される
            // Shrink requests are ignored
            service.ExpandSlots(10);
            Assert.AreEqual(54, service.GetSlotSize());
        }

        // レベル解放で既存プレイヤーのインベントリが拡張され、アイテムが保持される
        // Level unlock expands existing player inventories while preserving items
        [Test]
        public void UnlockLevelExpandsPlayerInventoryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();

            var inventory = inventoryDataStore.GetInventoryData(0);
            Assert.AreEqual(45, inventory.MainOpenableInventory.GetSlotSize());
            inventory.MainOpenableInventory.SetItem(44, new ItemId(1), 9);

            store.UnlockLevel(1);

            Assert.AreEqual(54, inventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(9, inventory.MainOpenableInventory.GetItem(44).Count);

            // レベル解放後に取得した新規プレイヤーも54スロット
            // A newly created player after the unlock also gets 54 slots
            var newPlayerInventory = inventoryDataStore.GetInventoryData(1);
            Assert.AreEqual(54, newPlayerInventory.MainOpenableInventory.GetSlotSize());
        }

        // 研究完了のclearedActionsでスロットレベルが解放される
        // Completing research unlocks the slot level via clearedActions
        [Test]
        public void ResearchClearedActionUnlocksSlotLevelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            Assert.AreEqual(45, store.CurrentSlotCount);

            ResearchDataStoreTest.CompleteResearchForTest(serviceProvider, System.Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-000000000001"));

            Assert.AreEqual(1, store.CurrentLevel);
            Assert.AreEqual(54, store.CurrentSlotCount);
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(ResearchDataStoreTest.PlayerId);
            Assert.AreEqual(54, inventory.MainOpenableInventory.GetSlotSize());
        }
    }
}
