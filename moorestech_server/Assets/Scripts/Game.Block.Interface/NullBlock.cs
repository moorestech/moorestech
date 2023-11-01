using System;
using Core.Const;
using Game.Block.Interface.State;

namespace Game.Block.Interface
{
    public class NullBlock : IBlock
    {
        public int EntityId => BlockConst.NullBlockEntityId;
        public int BlockId => BlockConst.EmptyBlockId;
        public ulong BlockHash => 0;
        public event Action<ChangedBlockState> OnBlockStateChange;


        public string GetSaveState()
        {
            return string.Empty;
        }
    }
}