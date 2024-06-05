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
        public IBlock New(BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new VanillaElectricPoleComponent(entityId);
            var components = new List<IBlockComponent>
            {
                transformer,
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new VanillaElectricPoleComponent(entityId);
            var components = new List<IBlockComponent>
            {
                transformer,
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
    }
}