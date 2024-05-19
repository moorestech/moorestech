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
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, entityId, blockPositionInfo);
        }
        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(config, entityId, blockPositionInfo);
        }

        private IBlock CreateGear(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = config.Param as ShaftConfigParam;
            var connectSetting = configParam.GearConnectSettings;

            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(configParam.LossPower, entityId, false, blockComponent);

            var components = new List<IBlockComponent>
            {
                gearEnergyTransformer,
                blockComponent,
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
    }
}