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
    public class VanillaShaftTemplate : IBlockTemplate
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
            var configParam = blockElement.BlockParam as ShaftBlockParam;
            var connectSetting = configParam.GearConnects;
            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(new Torque(configParam.RequireTorque), blockInstanceId, blockComponent);
            
            var components = new List<IBlockComponent>
            {
                gearEnergyTransformer,
                blockComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockElement.BlockId, components, blockPositionInfo);
        }
    }
}