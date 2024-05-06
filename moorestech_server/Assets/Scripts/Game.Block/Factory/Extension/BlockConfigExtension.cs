using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.Extension
{
    public static class BlockConfigExtension
    {
        public static BlockConnectorComponent<IBlockInventory> CreateConnector(this BlockConfigData config, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(config.InputConnectSettings, config.OutputConnectSettings, blockPositionInfo);
        }
    }
}