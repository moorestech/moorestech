using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricPole;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new VanillaElectricPoleComponent(blockInstanceId);
            var components = new List<IBlockComponent>
            {
                transformer,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new VanillaElectricPoleComponent(blockInstanceId);
            var components = new List<IBlockComponent>
            {
                transformer,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}