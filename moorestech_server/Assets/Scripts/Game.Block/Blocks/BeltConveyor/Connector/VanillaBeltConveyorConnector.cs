using System.Linq;
using Core.Item.Interface;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Connector
{
    public class VanillaBeltConveyorConnector : IBeltConveyorConnector
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        public VanillaBeltConveyorConnector(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return itemStack;
            
            var connector = targets.First();
            var output = connector.Key.InsertItem(itemStack);
            
            return output;
        }
    }
}