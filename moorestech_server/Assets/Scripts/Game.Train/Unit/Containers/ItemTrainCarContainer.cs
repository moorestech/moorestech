using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Train.Unit.Containers
{
    public class ItemTrainCarContainer : IOpenableInventory, IFuelProviderTrainCarContainer
    {
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        // 通知の宛先は装着中の列車。Attach/Detach経由でのみ更新される
        // The current owning car; populated only through Attach/Detach lifecycle hooks.
        private TrainCar _attachedCar;

        public IObservable<(int slot, IItemStack stack)> OnSlotChanged => _onSlotChangedSubject;
        private readonly Subject<(int slot, IItemStack stack)> _onSlotChangedSubject = new();


        private ItemTrainCarContainer(int slotNumber)
        {
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(
                (slot, itemStack) =>
                {
                    _attachedCar?.NotifyInventoryUpdate(slot, itemStack);
                    _onSlotChangedSubject.OnNext((slot, itemStack));
                }, ServerContext.ItemStackFactory, slotNumber);
        }

        private ItemTrainCarContainer(IItemStack[] inventoryItems) : this(inventoryItems.Length)
        {
            for (var i = 0; i < inventoryItems.Length; i++)
            {
                _itemDataStoreService.SetItemWithoutEvent(i, inventoryItems[i]);
            }
        }
        
        public (string containerType, string saveState) GetSaveState()
        {
            var items = InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList();
            return (TrainCarMasterElement.DefaultContainerTypeConst.Item, JsonConvert.SerializeObject(items));
        }

        public static ItemTrainCarContainer Load(string saveState, TrainCarMasterElement master)
        {
            var items = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(saveState) ?? new List<ItemStackSaveJsonObject>();
            return new ItemTrainCarContainer(BuildItemStacks(master.InventorySlots));

            #region Internal

            // マスタ定義に合わせて配列を切り詰め、もしくは拡張する
            // Truncate or extend the array to match the master definition.
            IItemStack[] BuildItemStacks(int slotCount)
            {
                var stacks = new IItemStack[slotCount];
                for (var i = 0; i < slotCount; i++)
                {
                    stacks[i] = i < items.Count ? items[i].ToItemStack() : ServerContext.ItemStackFactory.CreatEmpty();
                }
                return stacks;
            }

            #endregion
        }


        public void OnAttachedToCar(TrainCar trainCar)
        {
            _attachedCar = trainCar;
        }

        public void OnDetachedFromCar()
        {
            _attachedCar = null;
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
            var otherItems = new List<IItemStack>(other._itemDataStoreService.InventoryItems);
            return InsertionCheck(otherItems);
        }

        public void MergeFrom(ItemTrainCarContainer other)
        {
            var otherSlotCount = other._itemDataStoreService.GetSlotSize();
            for (var i = 0; i < otherSlotCount; i++)
            {
                var sourceItem = other._itemDataStoreService.GetItem(i);
                if (sourceItem.Id == ItemMaster.EmptyItemId) continue;

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
