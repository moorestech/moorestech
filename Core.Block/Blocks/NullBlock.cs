using System;
using Core.Const;

namespace Core.Block.Blocks
{
    public class NullBlock : IBlock
    {
        public NullBlock()
        {
        }

        public int GetIntId()
        {
            return BlockConst.NullBlockIntId;
        }

        public int GetBlockId()
        {
            return BlockConst.EmptyBlockId;
        }

        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}