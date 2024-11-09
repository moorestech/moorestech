using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerTransporterConnector : IBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        public ChainerTransporterConnector(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var context = CraftChainerManager.Instance.GetChainerNetworkContext();
            if (context == null)
            {
                return itemStack;
            }
            
            var target = context.GetTransportNextBlock(itemStack.ItemInstanceId, _blockConnectorComponent);
            if (target == null) return itemStack;
            
            return target.InsertItem(itemStack);
        }
    }
}