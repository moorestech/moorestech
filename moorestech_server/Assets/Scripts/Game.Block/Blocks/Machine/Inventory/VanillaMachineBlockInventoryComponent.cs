using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineBlockInventoryComponent : IOpenableBlockInventoryComponent
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        
        public VanillaMachineBlockInventoryComponent(VanillaMachineInputInventory vanillaMachineInputInventory, VanillaMachineOutputInventory vanillaMachineOutputInventory)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
        }
        
        public IReadOnlyList<IItemStack> InventoryItems
        {
            get
            {
                BlockException.CheckDestroy(this);
                var items = new List<IItemStack>();
                items.AddRange(_vanillaMachineInputInventory.InputSlot);
                items.AddRange(_vanillaMachineOutputInventory.OutputSlot);
                return items;
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
            
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
                return _vanillaMachineInputInventory.InputSlot[slot];
            
            slot -= _vanillaMachineInputInventory.InputSlot.Count;
            return _vanillaMachineOutputInventory.OutputSlot[slot];
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                _vanillaMachineInputInventory.SetItem(slot, itemStack);
            }
            else
            {
                slot -= _vanillaMachineInputInventory.InputSlot.Count;
                _vanillaMachineOutputInventory.SetItem(slot, itemStack);
            }
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
            
            return _vanillaMachineInputInventory.InputSlot.Count + _vanillaMachineOutputInventory.OutputSlot.Count;
        }
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            
            var items = new List<IItemStack>();
            items.AddRange(_vanillaMachineInputInventory.InputSlot);
            items.AddRange(_vanillaMachineOutputInventory.OutputSlot);
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
            
            ItemProcessResult result;
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                //アイテムIDが同じの時はスタックして余ったものを返す
                var item = _vanillaMachineInputInventory.InputSlot[slot];
                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _vanillaMachineInputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }
                
                //違う場合はそのまま入れ替える
                _vanillaMachineInputInventory.SetItem(slot, itemStack);
                return item;
            }
            else
            {
                //アウトプットスロットのインデックスに変換する
                slot -= _vanillaMachineInputInventory.InputSlot.Count;
                
                var item = _vanillaMachineOutputInventory.OutputSlot[slot];
                
                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _vanillaMachineOutputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }
                
                _vanillaMachineOutputInventory.SetItem(slot, itemStack);
                return item;
            }
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}