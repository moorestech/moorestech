using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     清浄機のフィルタースロットをUIとベルトへ公開し、フィルター以外を受け付けない
    ///     Exposes the purifier's filter slot to the UI and belts, refusing anything but filters
    /// </summary>
    public class CleanRoomAirFilterInventoryComponent : IOpenableBlockInventoryComponent
    {
        private readonly OpenableInventoryItemDataStoreService _filterSlot;
        private readonly ItemId _filterItemId;

        public CleanRoomAirFilterInventoryComponent(OpenableInventoryItemDataStoreService filterSlot, ItemId filterItemId)
        {
            _filterSlot = filterSlot;
            _filterItemId = filterItemId;
        }

        public IReadOnlyList<IItemStack> InventoryItems => _filterSlot.InventoryItems;

        public IItemStack GetItem(int slot)
        {
            CheckDestroy(this);
            return _filterSlot.GetItem(slot);
        }

        // 手動移動はこの判定で拒否され、移動サービスが理由をログと通知に出す
        // Manual moves are refused here and the move service logs and notifies the reason
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            return IsFilterOrEmpty(itemStack);
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

            // silent-ok: フィルター以外は受け取らず全量を差し戻す / ベルトの出口に品が留まって詰まるのをプレイヤーが見て気づく
            // silent-ok: non-filters are handed back whole / the player sees the item parked and clogging at the belt exit
            if (!IsFilterOrEmpty(itemStack)) return itemStack;
            return _filterSlot.InsertItem(itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);

            // フィルターだけをスロットへ渡し、それ以外は残りとして返す
            // Hand only filters to the slot and return the rest as remainders
            var filters = new List<IItemStack>();
            var rejected = new List<IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                if (IsFilterOrEmpty(itemStack)) filters.Add(itemStack);
                else rejected.Add(itemStack);
            }

            var remainders = _filterSlot.InsertItem(filters);
            remainders.AddRange(rejected);
            return remainders;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            foreach (var itemStack in itemStacks)
            {
                if (!IsFilterOrEmpty(itemStack)) return false;
            }

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

        private bool IsFilterOrEmpty(IItemStack itemStack)
        {
            return itemStack.Id == ItemMaster.EmptyItemId || itemStack.Id == _filterItemId;
        }
    }
}
