using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearElectric;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearElectricGeneratorTemplate : IBlockTemplate
    {
        private readonly IBlockRemover _blockRemover;

        public VanillaGearElectricGeneratorTemplate(IBlockRemover blockRemover)
        {
            _blockRemover = blockRemover;
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock Create(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as GearElectricGeneratorBlockParam;
            var gearConnects = param.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnects, gearConnects, blockPositionInfo);
            var overloadConfig = GearOverloadConfig.From(param);
            
            var components = new List<IBlockComponent>
            {
                new GearElectricGeneratorComponent(param, blockInstanceId, gearConnector, overloadConfig, _blockRemover),
                gearConnector,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
