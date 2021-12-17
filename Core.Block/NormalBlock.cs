using System;

namespace Core.Block
{
    public class NormalBlock : IBlock
    {
        private readonly int _blockId;
        private readonly int _intId;

        public NormalBlock(int blockId,int intId)
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

        public string GetState()
        {
            return String.Empty;
        }
    }
}