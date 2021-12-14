using System.Collections.Generic;

namespace Core.Block
{
    public class BlockConfig
    {
        private readonly Dictionary<int, BlockConfigData> BlockConfigDictionary;

        public BlockConfig(Dictionary<int, BlockConfigData> blockConfigDictionary)
        {
            BlockConfigDictionary = blockConfigDictionary;
        }

        public BlockConfigData GetBlockConfigData(int id)
        {
            return BlockConfigDictionary[id];
        }
    }
}