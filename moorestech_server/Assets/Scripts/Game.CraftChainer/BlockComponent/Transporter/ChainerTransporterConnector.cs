using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;

namespace Game.CraftChainer.BlockComponent
{
    /// <summary>
    /// そのアイテムがどのクラフトノードに挿入されるべきかを判断し、挿入するためのクラス
    /// Class for determining which craft node the item should be inserted into and inserting it
    /// </summary>
    public class ChainerTransporterConnector : IBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly CraftChainerNodeId _startChainerNodeId;
        
        public ChainerTransporterConnector(BlockConnectorComponent<IBlockInventory> blockConnectorComponent, CraftChainerNodeId startChainerNodeId)
        {
            _blockConnectorComponent = blockConnectorComponent;
            _startChainerNodeId = startChainerNodeId;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var context = CraftChainerManager.Instance.GetChainerNetworkContext(_startChainerNodeId);
            if (context == null)
            {
                return itemStack;
            }
            
            var target = context.GetTransportNextBlock(itemStack, _startChainerNodeId, _blockConnectorComponent);
            if (target == null) return itemStack;
            
            return target.InsertItem(itemStack);
        }
    }
}