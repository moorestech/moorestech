using System;
using Game.Block.Interface.State;

namespace Game.Block.Interface
{
    public interface IBlock
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        public string GetSaveState();

        public event Action<ChangedBlockState> OnBlockStateChange;
    }
}