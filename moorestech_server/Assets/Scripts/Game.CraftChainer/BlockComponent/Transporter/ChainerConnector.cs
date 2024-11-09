using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerConnector : IBeltConveyorConnector
    {
        private readonly ChainerNetworkContext _chainerNetworkContext;
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        public ChainerConnector(BlockConnectorComponent<IBlockInventory> blockConnectorComponent, ChainerNetworkContext chainerNetworkContext)
        {
            _blockConnectorComponent = blockConnectorComponent;
            _chainerNetworkContext = chainerNetworkContext;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var target = _chainerNetworkContext.GetTransportNextBlock(itemStack.ItemInstanceId, _blockConnectorComponent);
            if (target == null) return itemStack;
            
            return target.InsertItem(itemStack);
        }
    }
}