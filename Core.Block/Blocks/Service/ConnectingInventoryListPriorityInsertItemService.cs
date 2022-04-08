using System;
using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Const;
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

        private int _index = -1;
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < _blockInventories.Count && itemStack.Id != ItemConst.EmptyItemId; i++)
            {
                lock (_blockInventories)
                {
                    AddIndex();
                    itemStack = _blockInventories[_index].InsertItem(itemStack);
                }
            }

            return itemStack;
        }

        private void AddIndex()
        {
            _index++;
            if (_blockInventories.Count <= _index)
            {
                _index = 0;
            }
        }
    }
}