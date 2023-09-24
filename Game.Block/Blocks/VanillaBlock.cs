using System;
using Game.Block.Interface;
using Game.Block.Interface.State;

namespace Game.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        public VanillaBlock(int blockId, int entityId, ulong blockHash)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
        }
        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}