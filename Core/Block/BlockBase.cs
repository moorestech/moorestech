using System;

namespace industrialization.Core.Block
{
    public abstract class BlockBase
    {
        protected uint BlockID;
        protected uint intID;

        public uint BlockId => BlockID;

        public uint IntId => intID;

        protected BlockBase(uint blockId, uint intId)
        {
            this.BlockID = blockId;
            this.intID = intId;
        }
    }
}