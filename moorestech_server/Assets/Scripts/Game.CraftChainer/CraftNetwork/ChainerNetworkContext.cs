using Core.Item.Interface;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.CraftChainer.CraftNetwork
{
    public class ChainerNetworkContext
    {
        
        /// <summary>
        /// アイテムのIDとつながっているコネクターから、次にインサートすべきブロックを取得する
        /// Get the next block to insert from the connector connected to the item ID
        /// </summary>
        public IBlockInventory GetTransportNextBlock(ItemInstanceId item, BlockConnectorComponent<IBlockInventory> blockConnector)
        {
            // TODO
            return null;
        }
        
    }
}