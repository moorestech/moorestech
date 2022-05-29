using System.Collections.Generic;
using Core.Block.Config.LoadConfig;

namespace Core.Block.Config
{
    public interface IBlockConfig
    {
        public BlockConfigData GetBlockConfig(int id);
        public BlockConfigData GetBlockConfig(ulong blockHash);
        public int GetBlockConfigCount();
        public List<int> GetBlockIds(string modId);
    }
}