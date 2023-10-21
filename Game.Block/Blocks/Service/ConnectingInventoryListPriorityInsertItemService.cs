using System.Collections.Generic;
using Core.Const;
using Core.Item;
using Game.Block.BlockInventory;

namespace Game.Block.Blocks.Service
{
    /// <summary>
    ///     
    /// </summary>
    public class ConnectingInventoryListPriorityInsertItemService
    {
        private readonly List<IBlockInventory> _blockInventories;

        private int _index = -1;

        public ConnectingInventoryListPriorityInsertItemService(List<IBlockInventory> blockInventories)
        {
            _blockInventories = blockInventories;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _blockInventories.Count && itemStack.Id != ItemConst.EmptyItemId; i++)
                lock (_blockInventories)
                {
                    AddIndex();
                    itemStack = _blockInventories[_index].InsertItem(itemStack);
                }

            return itemStack;
        }

        private void AddIndex()
        {
            _index++;
            if (_blockInventories.Count <= _index) _index = 0;
        }
    }
}