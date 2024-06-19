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
    public class VanillaShaftTemplate : IBlockTemplate
    {
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, blockInstanceId, blockPositionInfo);
        }
        public IBlock New(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = config.Param as ShaftConfigParam;
            List<ConnectSettings> connectSetting = configParam.GearConnectSettings;
            
            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(configParam.RequireTorque, blockInstanceId, blockComponent);
            
            var components = new List<IBlockComponent>
            {
                gearEnergyTransformer,
                blockComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
    }
}