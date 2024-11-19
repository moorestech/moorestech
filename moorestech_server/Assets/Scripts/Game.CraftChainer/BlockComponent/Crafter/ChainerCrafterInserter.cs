using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;

namespace Game.CraftChainer.BlockComponent.Crafter
{
    public class ChainerCrafterInserter : IBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        private int _index = 0;
        
        public ChainerCrafterInserter(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var notChainerConnector = new List<IBlockInventory>();
            foreach (var connector in _blockConnectorComponent.ConnectedTargets)
            {
                var block = connector.Value.TargetBlock;
                if (block.ComponentManager.ExistsComponent<ICraftChainerNode>())
                {
                    continue;
                }
                
                notChainerConnector.Add(connector.Key);
            }
            
            _index++;
            if (notChainerConnector.Count <= _index)
            {
                _index = 0;
            }
            
            return notChainerConnector[_index].InsertItem(itemStack);
        }
    }
}