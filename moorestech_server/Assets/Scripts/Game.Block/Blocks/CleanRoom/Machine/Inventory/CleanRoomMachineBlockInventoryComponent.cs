using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaMachineBlockInventoryComponent をコピーし、出力側を CleanRoomMachineOutputInventory に差し替え。入力は Vanilla を再利用。
    // Copied from VanillaMachineBlockInventoryComponent; output side swapped to CleanRoomMachineOutputInventory, input reuses Vanilla.
    public class CleanRoomMachineBlockInventoryComponent : IOpenableBlockInventoryComponent, ISortExcludedSlots
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly CleanRoomMachineOutputInventory _outputInventory;
        private readonly VanillaMachineModuleInventory _moduleInventory;

        // 統合スロット順のサブインベントリ列
        // Sub-inventories in unified slot order
        private readonly IVanillaMachineSubInventory[] _subInventories;

        public CleanRoomMachineBlockInventoryComponent(VanillaMachineInputInventory inputInventory, CleanRoomMachineOutputInventory outputInventory, VanillaMachineModuleInventory moduleInventory)
        {
            _inputInventory = inputInventory;
            _outputInventory = outputInventory;
            _moduleInventory = moduleInventory;
            _subInventories = new IVanillaMachineSubInventory[] { inputInventory, outputInventory, moduleInventory };
        }

        public IReadOnlyList<IItemStack> InventoryItems
        {
            get
            {
                BlockException.CheckDestroy(this);
                var items = new List<IItemStack>();
                foreach (var subInventory in _subInventories) items.AddRange(subInventory.Items);
                return items;
            }
        }

        // モジュールスロットは整理対象から除外する
        // Module slots are excluded from sorting
        public IReadOnlyCollection<int> SortExcludedSlots
        {
            get
            {
                BlockException.CheckDestroy(this);

                var moduleSlotCount = _moduleInventory.ModuleSlot.Count;
                var moduleRangeStart = GetSlotSize() - moduleSlotCount;
                return Enumerable.Range(moduleRangeStart, moduleSlotCount).ToList();
            }
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);

            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            return ReplaceItem(slot, item);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inputInventory.InsertItem(itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);

            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            return _inputInventory.InsertItem(item);
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            return subInventory.Items[localSlot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            subInventory.SetItem(localSlot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);

            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            SetItem(slot, item);
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);

            return _subInventories.Sum(subInventory => subInventory.Items.Count);
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);

            var items = new List<IItemStack>();
            foreach (var subInventory in _subInventories) items.AddRange(subInventory.Items);
            return new ReadOnlyCollection<IItemStack>(items);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inputInventory.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inputInventory.InsertionCheck(itemStacks);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            var current = subInventory.Items[localSlot];

            // アイテムIDが同じ時はスタックして余りを返し、違う場合はそのまま入れ替える
            // Stack and return the remainder when IDs match; otherwise swap as-is
            if (current.Id == itemStack.Id)
            {
                var result = current.AddItem(itemStack);
                subInventory.SetItem(localSlot, result.ProcessResultItemStack);
                return result.RemainderItemStack;
            }

            subInventory.SetItem(localSlot, itemStack);
            return current;
        }

        // スロット番号をサブインベントリとローカル番号へ解決
        // Resolve a slot number to its sub-inventory and local index
        private (IVanillaMachineSubInventory subInventory, int localSlot) ResolveSlot(int slot)
        {
            foreach (var subInventory in _subInventories)
            {
                if (slot < subInventory.Items.Count) return (subInventory, slot);
                slot -= subInventory.Items.Count;
            }

            throw new ArgumentOutOfRangeException(nameof(slot), slot, "スロット番号がインベントリサイズを超えています。 The slot number exceeds the inventory size.");
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
