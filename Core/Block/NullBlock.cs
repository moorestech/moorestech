using System;

namespace industrialization.Core.Block
{
    public class NullBlock : BlockBase
    {
        public NullBlock(uint blockId, uint intId) : base(blockId, intId)
        {
            intId = Int32.MaxValue;
            blockId = 0;
        }
    }
}