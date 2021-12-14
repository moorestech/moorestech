using System;

namespace Core.Block
{
    public class NullBlock : IBlock
    {
        public NullBlock()
        {
        }
        
        public int GetIntId(){return BlockConst.BlockConst.NullBlockIntId;}
        public int GetBlockId() { return BlockConst.BlockConst.NullBlockId; }
        public IBlock New(BlockConfigData param)
        {
            return new NullBlock();
        }
    }
}