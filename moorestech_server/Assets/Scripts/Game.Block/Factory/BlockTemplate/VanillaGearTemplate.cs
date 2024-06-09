using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = config.Param as GearConfigParam;
            List<ConnectSettings> connectSetting = configParam.GearConnectSettings;
            
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearComponent = new GearComponent(configParam, blockInstanceId, gearConnector);
            
            var components = new List<IBlockComponent>
            {
                gearComponent,
                gearConnector,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
    }
}