using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;
using Core.Master;

namespace Core.Inventory
{
    /// <summary>
    ///     プレイヤーが開くことができるインベントリ系のインターフェース
    ///     プレイヤーのインベントリやブロックのインベントリが該当する
    /// </summary>
    public interface IOpenableInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems { get; }
        
        public IItemStack GetItem(int slot);
        void SetItem(int slot, IItemStack itemStack);
        void SetItem(int slot, ItemId itemId, int count);
        public IItemStack ReplaceItem(int slot, IItemStack itemStack);
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count);
        
        public IItemStack InsertItem(IItemStack itemStack);
        public IItemStack InsertItem(ItemId itemId, int count);
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks);
        public bool InsertionCheck(List<IItemStack> itemStacks);
        public int GetSlotSize();
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems();
    }
}