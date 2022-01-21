using System.Collections.Generic;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Item.Util;

namespace Core.Block.Config
{
    public class BlockConfig : IBlockConfig
    {
        private readonly Dictionary<int, BlockConfigData> _blockConfigDictionary;

        public BlockConfig()
        {
            _blockConfigDictionary = new BlockConfigJsonLoad(ConfigPath.ConfigPath.BlockConfigPath).LoadJson();
        }

        public BlockConfigData GetBlockConfig(int id)
        {
            if (_blockConfigDictionary.ContainsKey(id))
            {
                return _blockConfigDictionary[id];
            }

            //未定義の時はNullBlockConfigを返す
            return new BlockConfigData(id,
                "ID " + id + " is undefined",
                VanillaBlockType.Block,
                new NullBlockConfigParam(),
                ItemConst.NullItemId);
        }

        public List<int> GetBlockIds()
        {
            return new List<int>(_blockConfigDictionary.Keys);
        }
    }
}