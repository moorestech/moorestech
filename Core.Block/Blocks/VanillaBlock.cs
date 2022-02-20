using System;

namespace Core.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
        private readonly int _blockId;
        private readonly int _entityId;

        public VanillaBlock(int blockId, int entityId)
        {
            _entityId = entityId;
            _blockId = blockId;
        }

        public int GetEntityId()
        {
            return _entityId;
        }

        public int GetBlockId()
        {
            return _blockId;
        }

        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}