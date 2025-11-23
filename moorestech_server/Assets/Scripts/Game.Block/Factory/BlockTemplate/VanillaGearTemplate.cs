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
    public class VanillaGearTemplate : IBlockTemplate
    {
        private readonly IBlockRemover _blockRemover;

        public VanillaGearTemplate(IBlockRemover blockRemover)
        {
            _blockRemover = blockRemover;
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return CreateGear(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as GearBlockParam;
            var connectSetting = configParam.Gear.GearConnects;
            var overloadConfig = GearOverloadConfig.From(configParam);
            
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearComponent = new GearComponent(configParam, blockInstanceId, gearConnector, overloadConfig, _blockRemover);
            
            var components = new List<IBlockComponent>
            {
                gearComponent,
                gearConnector,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
