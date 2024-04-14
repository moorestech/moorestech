using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricPole;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new VanillaElectricPoleComponent(entityId);
            var components = new List<IBlockComponent>
            {
                transformer,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new VanillaElectricPoleComponent(entityId);
            var components = new List<IBlockComponent>
            {
                transformer,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }
    }
}