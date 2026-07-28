using System.Collections.Generic;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class EquipmentInventoryEventTest
    {
        private const int PlayerId = 0;

        [Test]
        public void スロット更新でインベントリ更新イベントが発火する()
        {
            var (inventoryDataStore, updateEvent) = CreateInventoryDataStoreWithEvent();
            var updatedProperties = new List<PlayerInventoryUpdateEventProperties>();
            updateEvent.Subscribe(properties => updatedProperties.Add(properties));

            var equipmentInventory = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;
            equipmentInventory.SetItem(1, ToolItemId(), 1);

            Assert.AreEqual(1, updatedProperties.Count);
            Assert.AreEqual(PlayerId, updatedProperties[0].PlayerId);
            Assert.AreEqual(1, updatedProperties[0].InventorySlot);
            Assert.AreEqual(ToolItemId(), updatedProperties[0].ItemStack.Id);
            Assert.AreEqual(1, updatedProperties[0].ItemStack.Count);
        }

        [Test]
        public void 選択インデックス変更でイベントが発火する()
        {
            var (inventoryDataStore, updateEvent) = CreateInventoryDataStoreWithEvent();
            var updatedProperties = new List<EquipmentSelectedIndexUpdateEventProperties>();
            updateEvent.SubscribeSelectedEquipmentIndex(properties => updatedProperties.Add(properties));

            var equipmentInventory = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;
            equipmentInventory.SetSelectedEquipmentIndex(1);
            equipmentInventory.SetSelectedEquipmentIndex(-1);

            Assert.AreEqual(2, updatedProperties.Count);
            Assert.AreEqual(PlayerId, updatedProperties[0].PlayerId);
            Assert.AreEqual(1, updatedProperties[0].SelectedEquipmentIndex);

            // 素手やクランプ後の値もそのまま通知される
            // Bare hands and clamped values are notified as they are
            Assert.AreEqual(-1, updatedProperties[1].SelectedEquipmentIndex);
        }

        [Test]
        public void 同じ選択インデックスを再設定しても発火しない()
        {
            var (inventoryDataStore, updateEvent) = CreateInventoryDataStoreWithEvent();
            var updateCount = 0;
            updateEvent.SubscribeSelectedEquipmentIndex(_ => updateCount++);

            var equipmentInventory = inventoryDataStore.GetInventoryData(PlayerId).EquipmentInventory;
            equipmentInventory.SetSelectedEquipmentIndex(1);
            equipmentInventory.SetSelectedEquipmentIndex(1);
            Assert.AreEqual(1, updateCount);

            // クランプ後に同値となる指定も変化なしとして扱う
            // Indexes that become the same value after clamping also count as no change
            equipmentInventory.SetSelectedEquipmentIndex(99);
            equipmentInventory.SetSelectedEquipmentIndex(MasterHolder.ToolMaster.EquipmentSlotCount + 5);
            Assert.AreEqual(2, updateCount);
        }

        [Test]
        public void セーブ復元では装備イベントを一切発火しない()
        {
            var (saveStore, _) = CreateInventoryDataStoreWithEvent();
            var savedEquipment = saveStore.GetInventoryData(PlayerId).EquipmentInventory;
            savedEquipment.SetItem(1, ToolItemId(), 1);
            savedEquipment.SetSelectedEquipmentIndex(2);
            var saveJsonObjects = saveStore.GetSaveJsonObject();

            // ロードは初期データ配布の前段なので、差分イベントをキューへ積んではいけない
            // Loading precedes the initial data handout, so it must not queue any diff event
            var (loadStore, loadUpdateEvent) = CreateInventoryDataStoreWithEvent();
            var inventoryUpdateCount = 0;
            var selectedIndexUpdateCount = 0;
            loadUpdateEvent.Subscribe(_ => inventoryUpdateCount++);
            loadUpdateEvent.SubscribeSelectedEquipmentIndex(_ => selectedIndexUpdateCount++);

            loadStore.LoadPlayerInventory(saveJsonObjects);

            var loadedEquipment = loadStore.GetInventoryData(PlayerId).EquipmentInventory;
            Assert.AreEqual(2, loadedEquipment.SelectedEquipmentIndex);
            Assert.AreEqual(ToolItemId(), loadedEquipment.GetItem(1).Id);
            Assert.AreEqual(0, inventoryUpdateCount);
            Assert.AreEqual(0, selectedIndexUpdateCount);
        }

        private (IPlayerInventoryDataStore inventoryDataStore, IEquipmentInventoryUpdateEvent updateEvent) CreateInventoryDataStoreWithEvent()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return (serviceProvider.GetService<IPlayerInventoryDataStore>(), serviceProvider.GetService<IEquipmentInventoryUpdateEvent>());
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(MasterHolder.ToolMaster.All[0].ToolItemGuid);
        }
    }
}
