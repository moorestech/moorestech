using System;

namespace Core.Block.Blocks
{
    public class NullBlock : IBlock
    {
        public NullBlock()
        {
        }

        public int GetIntId()
        {
            return BlockConst.BlockConst.NullBlockIntId;
        }

        public int GetBlockId()
        {
            return BlockConst.BlockConst.EmptyBlockId;
        }

        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}