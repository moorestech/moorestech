using System;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;

namespace Core.Block
{
    public class NullBlock : IBlock
    {
        public NullBlock()
        {
        }
        
        public int GetIntId(){return BlockConst.BlockConst.NullBlockIntId;}
        public int GetBlockId() { return BlockConst.BlockConst.NullBlockId; }
        public string GetSaveState()
        {
            return String.Empty;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            return new NullBlock();
        }
    }
}