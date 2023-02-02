using System.Collections.Generic;
using Core.Block.Config.LoadConfig;

namespace Core.Block.Config
{
    public interface IBlockConfig
    {
        public BlockConfigData GetBlockConfig(int id);
        public BlockConfigData GetBlockConfig(ulong blockHash);
        public BlockConfigData GetBlockConfig(string modId, string blockName);
        public int GetBlockConfigCount();
        public List<int> GetBlockIds(string modId);
    }
}