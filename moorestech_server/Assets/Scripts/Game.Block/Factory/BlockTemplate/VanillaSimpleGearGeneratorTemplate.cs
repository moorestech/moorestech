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
    public class VanillaSimpleGearGeneratorTemplate : IBlockTemplate
    {
        public IBlock Load(string state, BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, entityId, blockPositionInfo);
        }
        public IBlock New(BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, entityId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = config.Param as SimpleGearGeneratorParam;
            List<ConnectSettings> connectSetting = configParam.GearConnectSettings;
            
            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearComponent = new SimpleGearGeneratorComponent(configParam, entityId, blockComponent);
            
            var components = new List<IBlockComponent>
            {
                gearComponent,
                blockComponent,
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
    }
}