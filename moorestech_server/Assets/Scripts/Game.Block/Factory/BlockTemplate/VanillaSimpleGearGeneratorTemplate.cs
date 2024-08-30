using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaSimpleGearGeneratorTemplate : IBlockTemplate
    {
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(blockElement, blockInstanceId, blockPositionInfo);
        }
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(blockElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockElement.BlockParam as SimpleGearGeneratorBlockParam;
            var connectSetting = configParam.GearConnects;
            
            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearComponent = new SimpleGearGeneratorComponent(configParam, blockInstanceId, blockComponent);
            
            var components = new List<IBlockComponent>
            {
                gearComponent,
                blockComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockElement.BlockGuid, components, blockPositionInfo);
        }
    }
}