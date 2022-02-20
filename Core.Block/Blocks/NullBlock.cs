using System;
using Core.Const;

namespace Core.Block.Blocks
{
    public class NullBlock : IBlock
    {
        public NullBlock()
        {
        }

        public int GetEntityId()
        {
            return BlockConst.NullBlockEntityId;
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