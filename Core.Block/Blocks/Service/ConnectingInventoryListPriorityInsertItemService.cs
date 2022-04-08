using System;
using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Item;

namespace Core.Block.Blocks.Service
{
    /// <summary>
    /// 優先度を持ってアイテムを挿入する
    /// </summary>
    public class ConnectingInventoryListPriorityInsertItemService
    {
        private readonly List<IBlockInventory> _blockInventories;

        public ConnectingInventoryListPriorityInsertItemService(List<IBlockInventory> blockInventories)
        {
            _blockInventories = blockInventories;
        }

        private int index = 0;
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < _blockInventories.Count; i++)
            {
                lock (_blockInventories)
                {
                    AddIndex();
                    itemStack = _blockInventories[index].InsertItem(itemStack);
                }
            }

            return itemStack;
        }

        private void AddIndex()
        {
            index++;
            if (_blockInventories.Count <= index)
            {
                index = 0;
            }
        }
    }
}