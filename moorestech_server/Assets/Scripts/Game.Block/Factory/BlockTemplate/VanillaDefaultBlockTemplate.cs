using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            return new BlockSystem(entityId, config.BlockId, new List<IBlockComponent>(), blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            return new BlockSystem(entityId, config.BlockId, new List<IBlockComponent>(), blockPositionInfo);
        }
    }
}