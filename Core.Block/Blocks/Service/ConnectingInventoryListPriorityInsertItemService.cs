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

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return null;
        }
    }
}