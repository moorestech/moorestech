using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaItemShooterTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = config.Param as ItemShooterConfigParam;
            var inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            
            var direction = blockPositionInfo.BlockDirection;
            var chestComponent = new ItemShooterComponent(inputConnectorComponent, itemShooter);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = config.Param as ItemShooterConfigParam;
            var inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            
            var direction = blockPositionInfo.BlockDirection;
            var chestComponent = new ItemShooterComponent(state, inputConnectorComponent, itemShooter);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
    }
}