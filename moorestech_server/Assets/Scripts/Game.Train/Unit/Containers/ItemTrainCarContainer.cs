using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.TrainModule;

namespace Game.Train.Unit.Containers
{
    public class ItemTrainCarContainer : IOpenableInventory, ITrainCarContainer, IFuelProviderTrainCarContainer
    {
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;

        // 列車インベントリの更新通知。外部（InventoryItemMoveProtocol等）から差し込む。
        // External hook for inventory updates (assigned by InventoryItemMoveProtocol etc.).
        public Action<int, IItemStack> OnInventoryUpdated { get; set; }

        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        private ItemTrainCarContainer(int slotNumber)
        {
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotNumber);
        }

        private ItemTrainCarContainer(IItemStack[] inventoryItems) : this(inventoryItems.Length)
        {
            for (var i = 0; i < inventoryItems.Length; i++)
            {
                _itemDataStoreService.SetItemWithoutEvent(i, inventoryItems[i]);
            }
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            OnInventoryUpdated?.Invoke(slot, itemStack);
        }

        public int GetWeight()
        {
            return MasterHolder.TrainUnitMaster.Train.ItemContainer.Weight;
        }

        public bool IsFull()
        {
            return _itemDataStoreService.InventoryItems.All(stack => stack.Id != ItemMaster.EmptyItemId && stack.Count >= MasterHolder.ItemMaster.GetItemMaster(stack.Id).MaxStack);
        }

        public bool IsEmpty()
        {
            return _itemDataStoreService.InventoryItems.All(stack => stack.Id == ItemMaster.EmptyItemId || stack.Count == 0);
        }

        public double ConsumeFuel(TrainCar trainCar)
        {
            var slotCount = _itemDataStoreService.GetSlotSize();
            for (var i = 0; i < slotCount; i++)
            {
                foreach (var trainFuelItemsElement in trainCar.TrainCarMasterElement.TrainFuelItems ?? Array.Empty<TrainFuelItemsElement>())
                {
                    var fuelItemId = MasterHolder.ItemMaster.GetItemId(trainFuelItemsElement.ItemGuid);
                    var current = _itemDataStoreService.GetItem(i);
                    if (current.Id == fuelItemId && current.Count > 0)
                    {
                        // 燃料を1個消費して残りスタックを書き戻す
                        // Consume one fuel item and write the remaining stack back.
                        _itemDataStoreService.SetItem(i, current.SubItem(1));
                        return trainFuelItemsElement.FuelDurationPerItem;
                    }
                }
            }

            return 0;
        }

        public bool CanInsert(ItemTrainCarContainer other)
        {
            var otherItems = other._itemDataStoreService.InventoryItems;
            if (otherItems.All(stack => stack.Id == ItemMaster.EmptyItemId)) return false;

            var slotCount = _itemDataStoreService.GetSlotSize();
            for (var i = 0; i < otherItems.Count; i++)
            {
                var sourceItem = otherItems[i];
                if (sourceItem.Id == ItemMaster.EmptyItemId) continue;

                // 空スロットがあれば任意のアイテムを受け入れ可能
                // Any empty slot can accept any item.
                for (var j = 0; j < slotCount; j++)
                {
                    var slot = _itemDataStoreService.GetItem(j);
                    if (slot.Id == ItemMaster.EmptyItemId) return true;
                    if (slot.Id != sourceItem.Id) continue;

                    // 同一アイテムでスタックに余裕があれば挿入可能
                    // Same-item slot with remaining capacity accepts the item.
                    if (slot.Count < MasterHolder.ItemMaster.GetItemMaster(slot.Id).MaxStack) return true;
                }
            }

            return false;
        }

        public void MergeFrom(ItemTrainCarContainer other)
        {
            var otherSlotCount = other._itemDataStoreService.GetSlotSize();
            for (var i = 0; i < otherSlotCount; i++)
            {
                var sourceItem = other._itemDataStoreService.GetItem(i);
                if (sourceItem.Id == ItemMaster.EmptyItemId) continue;

                // 本コンテナへ挿入し、余ったぶんを元スロットへ戻す
                // Insert into this container and write the remainder back to the same slot.
                var remainder = _itemDataStoreService.InsertItem(sourceItem);
                other._itemDataStoreService.SetItem(i, remainder);
            }
        }

        public static ItemTrainCarContainer CreateWithEmptySlots(int size)
        {
            return new ItemTrainCarContainer(size);
        }

        public static ItemTrainCarContainer CreateWithInventoryItems(IItemStack[] inventoryItems)
        {
            return new ItemTrainCarContainer(inventoryItems);
        }

        #region IOpenableInventory

        public IItemStack GetItem(int slot) => _itemDataStoreService.GetItem(slot);
        public void SetItem(int slot, IItemStack itemStack) => _itemDataStoreService.SetItem(slot, itemStack);
        public void SetItem(int slot, ItemId itemId, int count) => _itemDataStoreService.SetItem(slot, itemId, count);
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) => _itemDataStoreService.ReplaceItem(slot, itemStack);
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) => _itemDataStoreService.ReplaceItem(slot, itemId, count);
        public IItemStack InsertItem(IItemStack itemStack) => _itemDataStoreService.InsertItem(itemStack);
        public IItemStack InsertItem(ItemId itemId, int count) => _itemDataStoreService.InsertItem(itemId, count);
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) => _itemDataStoreService.InsertItem(itemStacks);
        public bool InsertionCheck(List<IItemStack> itemStacks) => _itemDataStoreService.InsertionCheck(itemStacks);
        public int GetSlotSize() => _itemDataStoreService.GetSlotSize();
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() => _itemDataStoreService.CreateCopiedItems();

        #endregion
    }
}
