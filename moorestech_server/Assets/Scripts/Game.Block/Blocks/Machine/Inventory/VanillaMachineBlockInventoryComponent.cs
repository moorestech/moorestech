using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        private readonly VanillaMachineModuleInventory _vanillaMachineModuleInventory;

        public VanillaMachineBlockInventoryComponent(VanillaMachineInputInventory vanillaMachineInputInventory, VanillaMachineOutputInventory vanillaMachineOutputInventory, VanillaMachineModuleInventory vanillaMachineModuleInventory)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _vanillaMachineModuleInventory = vanillaMachineModuleInventory;
        }

        public IReadOnlyList<IItemStack> InventoryItems
        {
            get
            {
                BlockException.CheckDestroy(this);
                var items = new List<IItemStack>();
                items.AddRange(_vanillaMachineInputInventory.InputSlot);
                items.AddRange(_vanillaMachineOutputInventory.OutputSlot);
                items.AddRange(_vanillaMachineModuleInventory.ModuleSlot);
                return items;
            }
        }

        /// <summary>
        ///     モジュールスロット（第3レンジ）はインベントリ整理の対象から除外する
        ///     Module slots (the third range) are excluded from inventory sorting
        /// </summary>
        public IReadOnlyCollection<int> GetSortExcludedSlots()
        {
            BlockException.CheckDestroy(this);

            var moduleRangeStart = _vanillaMachineInputInventory.InputSlot.Count + _vanillaMachineOutputInventory.OutputSlot.Count;
            return Enumerable.Range(moduleRangeStart, _vanillaMachineModuleInventory.ModuleSlot.Count).ToList();
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
            
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
                return _vanillaMachineInputInventory.InputSlot[slot];

            slot -= _vanillaMachineInputInventory.InputSlot.Count;
            if (slot < _vanillaMachineOutputInventory.OutputSlot.Count)
                return _vanillaMachineOutputInventory.OutputSlot[slot];

            // アウトプットレンジを超えたスロットはモジュールレンジとして扱う
            // Slots beyond the output range are treated as the module range
            slot -= _vanillaMachineOutputInventory.OutputSlot.Count;
            return _vanillaMachineModuleInventory.GetItem(slot);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                _vanillaMachineInputInventory.SetItem(slot, itemStack);
                return;
            }

            slot -= _vanillaMachineInputInventory.InputSlot.Count;
            if (slot < _vanillaMachineOutputInventory.OutputSlot.Count)
            {
                _vanillaMachineOutputInventory.SetItem(slot, itemStack);
                return;
            }

            // アウトプットレンジを超えたスロットはモジュールレンジとして扱う
            // Slots beyond the output range are treated as the module range
            slot -= _vanillaMachineOutputInventory.OutputSlot.Count;
            _vanillaMachineModuleInventory.SetItem(slot, itemStack);
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
            
            return _vanillaMachineInputInventory.InputSlot.Count + _vanillaMachineOutputInventory.OutputSlot.Count + _vanillaMachineModuleInventory.ModuleSlot.Count;
        }
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            
            var items = new List<IItemStack>();
            items.AddRange(_vanillaMachineInputInventory.InputSlot);
            items.AddRange(_vanillaMachineOutputInventory.OutputSlot);
            items.AddRange(_vanillaMachineModuleInventory.ModuleSlot);
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
            
            // スロット番号をレンジごとのローカル番号へ変換し、共通の置き換え処理へ委譲する
            // Convert the slot number to a per-range local index and delegate to the shared replace logic
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
                return Replace(_vanillaMachineInputInventory.InputSlot[slot], item => _vanillaMachineInputInventory.SetItem(slot, item));

            slot -= _vanillaMachineInputInventory.InputSlot.Count;
            if (slot < _vanillaMachineOutputInventory.OutputSlot.Count)
                return Replace(_vanillaMachineOutputInventory.OutputSlot[slot], item => _vanillaMachineOutputInventory.SetItem(slot, item));

            slot -= _vanillaMachineOutputInventory.OutputSlot.Count;
            return Replace(_vanillaMachineModuleInventory.GetItem(slot), item => _vanillaMachineModuleInventory.SetItem(slot, item));

            #region Internal

            IItemStack Replace(IItemStack current, Action<IItemStack> setItem)
            {
                // アイテムIDが同じ時はスタックして余りを返し、違う場合はそのまま入れ替える
                // Stack and return the remainder when IDs match; otherwise swap the items as-is
                if (current.Id == itemStack.Id)
                {
                    var result = current.AddItem(itemStack);
                    setItem(result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                setItem(itemStack);
                return current;
            }

            #endregion
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}