using System;

namespace industrialization.Core.Block
{
    public class NullBlock : IBlock
    {
        public NullBlock(int blockId, int intId)
        {
        }
        
        public int GetIntId(){return Int32.MaxValue;}
        public int GetBlockId() { return BlockConst.NullBlockId; }
    }
}