using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var chest = config.Param as ChestConfigParam;
            var inputConnectorComponent = config.CreateConnector(blockPositionInfo);
            var chestComponent = new VanillaChestComponent(entityId, chest.ChestItemNum, inputConnectorComponent);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var chest = config.Param as ChestConfigParam;
            var inputConnectorComponent = config.CreateConnector(blockPositionInfo);
            var chestComponent = new VanillaChestComponent(state, entityId, chest.ChestItemNum, inputConnectorComponent);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
    }
}