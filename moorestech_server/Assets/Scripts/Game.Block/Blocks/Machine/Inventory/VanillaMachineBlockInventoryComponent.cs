using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineBlockInventoryComponent : IOpenableBlockInventoryComponent, ISortExcludedSlots
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        // 統合スロット順のサブインベントリ列
        // Sub-inventories in unified slot order
        private readonly IVanillaMachineSubInventory[] _subInventories;

        public VanillaMachineBlockInventoryComponent(VanillaMachineInputInventory vanillaMachineInputInventory, VanillaMachineOutputInventory vanillaMachineOutputInventory, VanillaMachineModuleInventory vanillaMachineModuleInventory)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _subInventories = new IVanillaMachineSubInventory[] { vanillaMachineInputInventory, vanillaMachineOutputInventory, vanillaMachineModuleInventory };
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

        // スロットは全て束縛済みで整理対象にならない（ADR 0042）
        // Every slot is recipe-bound, so none participates in sorting (ADR 0042)
        public IReadOnlyCollection<int> SortExcludedSlots
        {
            get
            {
                BlockException.CheckDestroy(this);
                return Enumerable.Range(0, GetSlotSize()).ToList();
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

            // アイテムをインプットスロットに入れた後、プロセス開始できるなら開始
            // Insert item into input slot, then start process if possible
            var item = _vanillaMachineInputInventory.InsertItem(itemStack);
            return item;
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);

            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            return _vanillaMachineInputInventory.InsertItem(item);
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            return subInventory.Items[localSlot];
        }

        // 入れ替え経路（move service の全量swap）も束縛を守る。ロード復元はサブインベントリの SetItemWithoutEvent を使うため影響しない
        // The swap path (full-stack swap in the move service) also honors the binding; load restore uses the sub-inventory's SetItemWithoutEvent and is unaffected
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            if (!subInventory.IsAllowedToPlace(localSlot, itemStack)) return;
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

            //アイテムをインプットスロットに入れた後、プロセス開始できるなら開始
            return _vanillaMachineInputInventory.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);

            return _vanillaMachineInputInventory.InsertionCheck(itemStacks);
        }

        /// <summary>
        ///     アイテムの置き換えを実行しますが、同じアイテムIDの場合はそのまま現在のアイテムにスタックされ、スタックしきらなかったらその分を返します。
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="itemStack"></param>
        /// <returns></returns>
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);

            // 束縛外のスロットへは置けず、そのまま返す（プレイヤー移動プロトコルの入口）
            // A stack that violates the binding bounces back untouched (entry point of the player move protocol)
            if (!subInventory.IsAllowedToPlace(localSlot, itemStack)) return itemStack;

            var current = subInventory.Items[localSlot];

            // アイテムIDが同じ時はスタックして余りを返し、違う場合はそのまま入れ替える
            // Stack and return the remainder when IDs match; otherwise swap the items as-is
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
            // 負のスロットは境界で弾き、ローカル番号が負になるのを防ぐ
            // Reject negative slots at the boundary to avoid a negative local index
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "スロット番号が負の値です。 The slot number is negative.");
            }

            var requestedSlot = slot;
            foreach (var subInventory in _subInventories)
            {
                if (slot < subInventory.Items.Count) return (subInventory, slot);
                slot -= subInventory.Items.Count;
            }

            throw new ArgumentOutOfRangeException(nameof(slot), requestedSlot, "スロット番号がインベントリサイズを超えています。 The slot number exceeds the inventory size.");
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
