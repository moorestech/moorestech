using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor
{
    public class VanillaBeltConveyorBlockInventoryInserter : IBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;

        public VanillaBeltConveyorBlockInventoryInserter(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return itemStack;

            var connector = targets.First();

            // ConnectedInfoからBlockConnectInfoElementを取得
            // Get BlockConnectInfoElement from ConnectedInfo
            var context = new InsertItemContext(connector.Value.SelfConnector, connector.Value.TargetConnector);
            
            return  connector.Key.InsertItem(itemStack, context);
        }
    }
}