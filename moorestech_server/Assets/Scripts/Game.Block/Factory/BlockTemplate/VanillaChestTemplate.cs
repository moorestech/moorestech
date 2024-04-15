using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            var inputConnectorComponent = CreateConnector(blockPositionInfo);
            var chestComponent = new VanillaChestComponent(entityId, chest.ChestItemNum, inputConnectorComponent);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            var inputConnectorComponent = CreateConnector(blockPositionInfo);
            var chestComponent  = new VanillaChestComponent(state, entityId, chest.ChestItemNum, inputConnectorComponent);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }
        
        private BlockConnectorComponent<IBlockInventory> CreateConnector(BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor }), blockPositionInfo);
        }
    }
}