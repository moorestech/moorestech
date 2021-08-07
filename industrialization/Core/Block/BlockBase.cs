using System;

namespace industrialization.Core.Block
{
    public abstract class BlockBase
    {
        protected int BlockID;
        protected int intID;

        public int BlockId => BlockID;

        public int IntId => intID;

        protected BlockBase(int blockId, int intId)
        {
            this.BlockID = blockId;
            this.intID = intId;
        }
    }
}