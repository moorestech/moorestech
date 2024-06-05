using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMachineTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            return new BlockSystem(entityId, config.BlockId, new List<IBlockComponent>(), blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            return new BlockSystem(entityId, config.BlockId, new List<IBlockComponent>(), blockPositionInfo);
        }
    }
}