using System.Collections.Generic;

namespace Game.Block.Interface.BlockConfig
{
    public interface IBlockConfig
    {
        public BlockConfigData GetBlockConfig(int id);
        public BlockConfigData GetBlockConfig(long blockHash);
        public BlockConfigData GetBlockConfig(string modId, string blockName);
        public int GetBlockConfigCount();
        public List<int> GetBlockIds(string modId);

        public bool IsBlock(int itemId);
        public int ItemIdToBlockId(int itemId);
        public int BlockIdToItemId(int blockId);
        public BlockConfigData ItemIdToBlockConfig(int itemId);
    }
}