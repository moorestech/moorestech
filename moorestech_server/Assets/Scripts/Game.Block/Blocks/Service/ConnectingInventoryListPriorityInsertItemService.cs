using System.Collections.Generic;
using Core.Const;
using Core.Item.Interface;
using Game.Block.BlockInventory;
using Game.Block.Component.IOConnector;

namespace Game.Block.Blocks.Service
{
    /// <summary>
    ///     順番にアイテムに入れ続けるシステム
    /// </summary>
    public class ConnectingInventoryListPriorityInsertItemService
    {
        private readonly InventoryInputConnectorComponent _inventoryInputConnectorComponent;

        private int _index = -1;

        public ConnectingInventoryListPriorityInsertItemService(InventoryInputConnectorComponent inventoryInputConnectorComponent)
        {
            _inventoryInputConnectorComponent = inventoryInputConnectorComponent;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            IReadOnlyList<IBlockInventory> inventories = _inventoryInputConnectorComponent.ConnectInventory;

            for (var i = 0; i < inventories.Count && itemStack.Id != ItemConst.EmptyItemId; i++)
                lock (inventories)
                {
                    AddIndex();
                    itemStack = inventories[_index].InsertItem(itemStack);
                }

            return itemStack;
        }

        private void AddIndex()
        {
            _index++;
            if (_inventoryInputConnectorComponent.ConnectInventory.Count <= _index) _index = 0;
        }
    }
}