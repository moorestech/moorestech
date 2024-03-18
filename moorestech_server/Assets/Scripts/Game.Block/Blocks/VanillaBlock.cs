using System;
using Game.Block.Base;
using Game.Block.Interface;
using Game.Block.Interface.State;

namespace Game.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
        public VanillaBlock(int blockId, int entityId, long blockHash)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
        }

        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        public string GetSaveState()
        {
            return string.Empty;
        }
    }
}