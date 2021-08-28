using System;

namespace industrialization.Core.Block
{
    public class NullBlock : BlockBase
    {
        public NullBlock(int blockId, int intId) : base(blockId, intId)
        {
            intId = Int32.MaxValue;
            blockId = BlockConst.NullIntId;
        }
    }
}