using System;
using System.Collections.Generic;
using System.Diagnostics;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Tests.UnitTest.Core.Inventory
{
    public class OpenableInventorySlotIndexTest
    {
        private IItemStackFactory _itemStackFactory;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _itemStackFactory = ServerContext.ItemStackFactory;
        }

        [Test]
        public void NonEmptySlotIndexTracksSlotMutations()
        {
            var inventory = CreateInventory(4);
            var item = _itemStackFactory.Create(new ItemId(1), 1);

            // slot更新に追従して非空indexが増減することを確認する
            // Verify that the non-empty index follows slot mutations
            Assert.AreEqual(0, inventory.NonEmptySlotIndexes.Count);
            inventory.SetItemWithoutEvent(2, item);
            Assert.AreEqual(1, inventory.NonEmptySlotIndexes.Count);
            Assert.AreEqual(2, inventory.NonEmptySlotIndexes[0]);

            inventory.SetItemWithoutEvent(2, _itemStackFactory.CreatEmpty());
            Assert.AreEqual(0, inventory.NonEmptySlotIndexes.Count);
        }

        [Test]
        public void InsertableCacheTracksFullAndPartialSlots()
        {
            var inventory = CreateInventory(1);
            var itemId = new ItemId(1);
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(itemId).MaxStack;

            // 満杯slotだけなら受け入れ不可、未満杯なら受け入れ可能になる
            // Full slots reject insertion while partial slots accept it
            inventory.SetItemWithoutEvent(0, _itemStackFactory.Create(itemId, maxStack));
            Assert.IsFalse(inventory.HasInsertableSlot);
            Assert.IsFalse(inventory.CanInsertItem(_itemStackFactory.Create(itemId, 1)));

            inventory.SetItemWithoutEvent(0, _itemStackFactory.Create(itemId, maxStack - 1));
            Assert.IsTrue(inventory.HasInsertableSlot);
            Assert.IsTrue(inventory.CanInsertItem(_itemStackFactory.Create(itemId, 1)));
        }

        [Test]
        [Explicit("Performance benchmark for manual before/after measurement.")]
        public void BenchmarkLegacyScanAndIndexedCache()
        {
            const int slotCount = 65536;
            const int nonEmptyCount = 16;
            const int iterations = 200;
            var itemId = new ItemId(1);
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(itemId).MaxStack;
            var sourceInventory = CreateSparseSourceInventory(slotCount, nonEmptyCount, itemId);
            var fullDestination = CreateFullDestinationInventory(slotCount, itemId, maxStack);
            var legacyInsertDestination = CreateRawFullExceptLastSlot(slotCount, itemId, maxStack);
            var indexedInsertDestination = CreateFullExceptLastSlot(slotCount, itemId, maxStack);
            var insertItem = _itemStackFactory.Create(itemId, 1);
            var emptyItem = _itemStackFactory.CreatEmpty();

            // 旧相当の全slot走査と新しいindex/cache参照を同条件で測る
            // Measure legacy full-slot scans and new index/cache access under identical data
            var legacySourceTicks = Measure(iterations, () => LegacySourceScan(sourceInventory));
            var indexedSourceTicks = Measure(iterations, () => IndexedSourceScan(sourceInventory));
            var legacyDestinationTicks = Measure(iterations, () => LegacyDestinationScan(fullDestination));
            var cachedDestinationTicks = Measure(iterations, () => CachedDestinationCheck(fullDestination));
            var legacyInsertTicks = Measure(iterations, () =>
            {
                var remaining = LegacyInsertToLastEmptySlot(legacyInsertDestination, insertItem);
                legacyInsertDestination[slotCount - 1] = emptyItem;
                return remaining;
            });
            var indexedInsertTicks = Measure(iterations, () =>
            {
                var remaining = indexedInsertDestination.InsertItemByIndex(insertItem);
                indexedInsertDestination.SetItemWithoutEvent(slotCount - 1, emptyItem);
                return remaining;
            });

            var legacySourceUs = ToMicrosecondsPerIteration(legacySourceTicks, iterations);
            var indexedSourceUs = ToMicrosecondsPerIteration(indexedSourceTicks, iterations);
            var legacyDestinationUs = ToMicrosecondsPerIteration(legacyDestinationTicks, iterations);
            var cachedDestinationUs = ToMicrosecondsPerIteration(cachedDestinationTicks, iterations);
            var legacyInsertUs = ToMicrosecondsPerIteration(legacyInsertTicks, iterations);
            var indexedInsertUs = ToMicrosecondsPerIteration(indexedInsertTicks, iterations);

            Debug.Log($"[ChestOptimizationBenchmark] slots={slotCount} nonEmpty={nonEmptyCount} iterations={iterations} sourceScan {legacySourceUs:F3}us -> {indexedSourceUs:F3}us speedup={legacySourceUs / indexedSourceUs:F1}x");
            Debug.Log($"[ChestOptimizationBenchmark] slots={slotCount} fullDestination=true iterations={iterations} destinationLock {legacyDestinationUs:F3}us -> {cachedDestinationUs:F3}us speedup={legacyDestinationUs / cachedDestinationUs:F1}x");
            Debug.Log($"[ChestOptimizationBenchmark] slots={slotCount} lastSlotEmpty=true iterations={iterations} chestToChestInsert {legacyInsertUs:F3}us -> {indexedInsertUs:F3}us speedup={legacyInsertUs / indexedInsertUs:F1}x");
        }

        private OpenableInventoryItemDataStoreService CreateInventory(int slotCount) => new((slot, item) => { }, _itemStackFactory, slotCount);

        private OpenableInventoryItemDataStoreService CreateSparseSourceInventory(int slotCount, int nonEmptyCount, ItemId itemId)
        {
            var inventory = CreateInventory(slotCount);
            var interval = slotCount / nonEmptyCount;
            for (var i = 0; i < nonEmptyCount; i++) inventory.SetItemWithoutEvent(i * interval, _itemStackFactory.Create(itemId, 1));
            return inventory;
        }

        private OpenableInventoryItemDataStoreService CreateFullDestinationInventory(int slotCount, ItemId itemId, int maxStack)
        {
            var inventory = CreateInventory(slotCount);
            for (var i = 0; i < slotCount; i++) inventory.SetItemWithoutEvent(i, _itemStackFactory.Create(itemId, maxStack));
            return inventory;
        }

        private OpenableInventoryItemDataStoreService CreateFullExceptLastSlot(int slotCount, ItemId itemId, int maxStack)
        {
            var inventory = CreateInventory(slotCount);
            for (var i = 0; i < slotCount - 1; i++) inventory.SetItemWithoutEvent(i, _itemStackFactory.Create(itemId, maxStack));
            return inventory;
        }

        private List<IItemStack> CreateRawFullExceptLastSlot(int slotCount, ItemId itemId, int maxStack)
        {
            var items = new List<IItemStack>();
            for (var i = 0; i < slotCount - 1; i++) items.Add(_itemStackFactory.Create(itemId, maxStack));
            items.Add(_itemStackFactory.CreatEmpty());
            return items;
        }

        private static int LegacySourceScan(OpenableInventoryItemDataStoreService inventory)
        {
            var count = 0;
            for (var i = 0; i < inventory.InventoryItems.Count; i++)
            {
                var itemStack = inventory.InventoryItems[i];
                if (itemStack.Id != ItemMaster.EmptyItemId && itemStack.Count != 0) count++;
            }

            return count;
        }

        private static int IndexedSourceScan(OpenableInventoryItemDataStoreService inventory) => inventory.NonEmptySlotIndexes.Count;

        private static bool LegacyDestinationScan(OpenableInventoryItemDataStoreService inventory)
        {
            for (var i = 0; i < inventory.InventoryItems.Count; i++)
            {
                var itemStack = inventory.InventoryItems[i];
                if (itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count == 0) return true;
                if (itemStack.Count < MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).MaxStack) return true;
            }

            return false;
        }

        private static bool CachedDestinationCheck(OpenableInventoryItemDataStoreService inventory) => inventory.HasInsertableSlot;

        private static IItemStack LegacyInsertToLastEmptySlot(List<IItemStack> inventoryItems, IItemStack itemStack)
        {
            for (var i = 0; i < inventoryItems.Count; i++)
            {
                if (!inventoryItems[i].IsAllowedToAddWithRemain(itemStack)) continue;
                var result = inventoryItems[i].AddItem(itemStack);
                if (inventoryItems[i].Equals(result.ProcessResultItemStack) && result.RemainderItemStack.Equals(itemStack)) continue;

                inventoryItems[i] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }

            return itemStack;
        }

        private static long Measure(int iterations, Func<object> action)
        {
            action();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++) action();
            stopwatch.Stop();
            return stopwatch.ElapsedTicks;
        }

        private static double ToMicrosecondsPerIteration(long ticks, int iterations) => ticks * 1000000.0 / Stopwatch.Frequency / iterations;
    }
}
