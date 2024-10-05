using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Interface;
using Core.Item.Util;
using Core.Master;
using Game.Block.Interface.Component;

namespace Tests.Module
{
    public class DummyBlockInventory : IBlockInventory
    {
        private readonly List<IItemStack> _insertedItems;
        
        private int _endInsertCnt;
        
        public DummyBlockInventory(int insertToEndNum = 1, int maxSlot = 100)
        {
            IsItemExists = false;
            InsertToEndNum = insertToEndNum;
            _endInsertCnt = 0;
            _insertedItems = CreateEmptyItemStacksList.Create(maxSlot).ToList();
        }
        
        public bool IsItemExists { get; private set; }
        
        public List<IItemStack> InsertedItems
        {
            get
            {
                var a = _insertedItems.Where(i => i.Id != ItemMaster.EmptyItemId).ToList();
                a.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
                return a.ToList();
            }
        }
        
        private int InsertToEndNum { get; }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _insertedItems.Count; i++)
            {
                if (!_insertedItems[i].IsAllowedToAdd(itemStack)) continue;
                var r = _insertedItems[i].AddItem(itemStack);
                _insertedItems[i] = r.ProcessResultItemStack;
                _endInsertCnt++;
                IsItemExists = InsertToEndNum <= _endInsertCnt;
                
                return r.RemainderItemStack;
            }
            
            return itemStack;
        }
        
        public IItemStack GetItem(int slot)
        {
            return _insertedItems[slot];
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            _insertedItems[slot] = itemStack;
        }
        
        public int GetSlotSize()
        {
            return _insertedItems.Count;
        }
        
        public bool IsDestroy => false;
        
        public void Destroy()
        {
        }
        
        public void AddOutputConnector(IBlockInventory blockInventory)
        {
        }
        
        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
        }
    }
}