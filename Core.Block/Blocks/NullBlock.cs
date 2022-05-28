using System;
using Core.Const;

namespace Core.Block.Blocks
{
    public class NullBlock : IBlock
    {
        public int EntityId => BlockConst.NullBlockEntityId;
        public int BlockId =>BlockConst.EmptyBlockId;
        public ulong BlockHash => 0;


        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}