using System;

namespace Core.Block
{
    public class NullBlock : IBlock
    {
        public NullBlock(int blockId, int intId)
        {
        }
        
        public int GetIntId(){return BlockConst.BlockConst.NullBlockIntId;}
        public int GetBlockId() { return BlockConst.BlockConst.NullBlockId; }
    }
}