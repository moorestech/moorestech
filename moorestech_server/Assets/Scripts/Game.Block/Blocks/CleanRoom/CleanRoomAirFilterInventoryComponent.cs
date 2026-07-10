using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     清浄機のフィルタースロットをUIへ公開する開閉インベントリ
    ///     Openable inventory exposing the purifier's filter slot to the UI
    /// </summary>
    public class CleanRoomAirFilterInventoryComponent : IOpenableBlockInventoryComponent
    {
        private readonly OpenableInventoryItemDataStoreService _filterSlot;

        public CleanRoomAirFilterInventoryComponent(OpenableInventoryItemDataStoreService filterSlot)
        {
            _filterSlot = filterSlot;
        }

        public IReadOnlyList<IItemStack> InventoryItems => _filterSlot.InventoryItems;

        public IItemStack GetItem(int slot)
        {
            CheckDestroy(this);
            return _filterSlot.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            _filterSlot.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            _filterSlot.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            return _filterSlot.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            return _filterSlot.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);
            return _filterSlot.InsertItem(itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            CheckDestroy(this);
            return _filterSlot.InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            CheckDestroy(this);
            return _filterSlot.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            return _filterSlot.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            return _filterSlot.InsertionCheck(itemStacks);
        }

        public int GetSlotSize()
        {
            CheckDestroy(this);
            return _filterSlot.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            CheckDestroy(this);
            return _filterSlot.CreateCopiedItems();
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
