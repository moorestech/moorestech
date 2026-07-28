using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Tests.UnitTest.Game.Inventory
{
    /// <summary>
    ///     受入制限を宣言するテスト用インベントリ。実体はOpenableInventoryItemDataStoreServiceへ委譲する
    ///     Test inventory declaring acceptance restrictions; delegates storage to OpenableInventoryItemDataStoreService
    /// </summary>
    public class FakeAcceptanceInventory : IOpenableInventory, IItemAcceptanceInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => _openableInventoryService.InventoryItems;

        private readonly IReadOnlyList<ItemId> _acceptableItemIds;
        private readonly int _maxCountPerSlot;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;

        public FakeAcceptanceInventory(IReadOnlyList<ItemId> acceptableItemIds, int maxCountPerSlot, int slotCount)
        {
            _acceptableItemIds = acceptableItemIds;
            _maxCountPerSlot = maxCountPerSlot;

            // 受入制限は自身を判定元としてストアサービスへ渡し、インベントリ自身に守らせる
            // Pass itself as the acceptance source so the store service enforces the restriction
            var option = new OpenableInventoryItemDataStoreServiceOption(this);
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotCount, option);
        }

        public bool CanAccept(ItemId itemId)
        {
            foreach (var acceptableItemId in _acceptableItemIds)
                if (acceptableItemId == itemId)
                    return true;

            return false;
        }

        public int GetMaxCountPerSlot(ItemId itemId)
        {
            return _maxCountPerSlot;
        }

        public IItemStack GetItem(int slot)
        {
            return _openableInventoryService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _openableInventoryService.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            _openableInventoryService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _openableInventoryService.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            return _openableInventoryService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _openableInventoryService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return _openableInventoryService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _openableInventoryService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _openableInventoryService.InsertionCheck(itemStacks);
        }

        public int GetSlotSize()
        {
            return _openableInventoryService.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return _openableInventoryService.CreateCopiedItems();
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
        }
    }
}
