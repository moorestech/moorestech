using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return new BlockSystem(blockInstanceId, blockElement.BlockId, new List<IBlockComponent>(), blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return new BlockSystem(blockInstanceId, blockElement.BlockId, new List<IBlockComponent>(), blockPositionInfo);
        }
    }
}