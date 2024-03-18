using System;
using Core.Const;
using Game.Block.Interface;
using Game.Block.Interface.State;

namespace Game.Block.Base
{
    public class NullBlock : IBlock
    {
        public int EntityId => BlockConst.NullBlockEntityId;
        public int BlockId => BlockConst.EmptyBlockId;
        public long BlockHash => 0;
        public event Action<ChangedBlockState> OnBlockStateChange;


        public string GetSaveState()
        {
            return string.Empty;
        }
    }
}