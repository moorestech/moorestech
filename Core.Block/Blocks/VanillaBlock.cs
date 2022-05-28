using System;

namespace Core.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }

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