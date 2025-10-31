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
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return CreateGear(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as ShaftBlockParam;
            var connectSetting = configParam.Gear.GearConnects;
            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(new Torque(configParam.RequireTorque), blockInstanceId, blockComponent);
            
            var components = new List<IBlockComponent>
            {
                gearEnergyTransformer,
                blockComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}