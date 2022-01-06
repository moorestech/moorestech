using System;

namespace Core.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
        private readonly int _blockId;
        private readonly int _intId;

        public VanillaBlock(int blockId, int intId)
        {
            _intId = intId;
            _blockId = blockId;
        }

        public int GetIntId()
        {
            return _intId;
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