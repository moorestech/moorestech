using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Service
{
    /// <summary>
    ///     順番にアイテムに入れ続けるシステム
    /// </summary>
    public class ConnectingInventoryListPriorityInsertItemService : IBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        private int _index = -1;
        
        public ConnectingInventoryListPriorityInsertItemService(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            IReadOnlyList<IBlockInventory> inventories = _blockConnectorComponent.ConnectedTargets.Keys.ToArray();
            
            for (var i = 0; i < inventories.Count && itemStack.Id != ItemMaster.EmptyItemId; i++)
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
            if (_blockConnectorComponent.ConnectedTargets.Count <= _index) _index = 0;
        }
    }
}