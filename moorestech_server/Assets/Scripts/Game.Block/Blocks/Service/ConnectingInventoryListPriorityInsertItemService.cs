using Core.Const;
using Core.Item.Interface;
using Game.Block.Component.IOConnector;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Service
{
    /// <summary>
    ///     順番にアイテムに入れ続けるシステム
    /// </summary>
    public class ConnectingInventoryListPriorityInsertItemService
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        private int _index = -1;
        
        public ConnectingInventoryListPriorityInsertItemService(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var inventories = _blockConnectorComponent.ConnectTargets;
            
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
            if (_blockConnectorComponent.ConnectTargets.Count <= _index) _index = 0;
        }
    }
}