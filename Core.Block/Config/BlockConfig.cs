using System;
using System.Collections.Generic;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.ConfigJson;
using Core.Const;
using Core.Item.Util;

namespace Core.Block.Config
{
    public class BlockConfig : IBlockConfig
    {
        private readonly List<BlockConfigData> _blockConfigList;
        private readonly Dictionary<ulong, BlockConfigData> _bockHashToConfig = new();

        public BlockConfig(ConfigJsonList configJson)
        {
            _blockConfigList = new BlockConfigJsonLoad().LoadFromJsons(configJson.BlockConfigs,configJson.SortedModIds);            
            foreach (var blockConfig in _blockConfigList)
            {
                _bockHashToConfig.Add(blockConfig.BlockHash, blockConfig);
            }
        }

        public BlockConfigData GetBlockConfig(int id)
        {
            //0は空気ブロックなので1を引いておくs
            id -= 1;
            if (id < 0)
            {
                throw new ArgumentException("id must be greater than 0 ID:" + id);
            }
            if (id < _blockConfigList.Count)
            {
                return _blockConfigList[id];
            }

            
            
            //未定義の時はNullBlockConfigを返す
            //idを元に戻す
            id++;
            return new BlockConfigData(id,
                "ID " + id + " is undefined",
                0,
                VanillaBlockType.Block,
                new NullBlockConfigParam(),
                ItemConst.EmptyItemId);
        }

        public BlockConfigData GetBlockConfig(ulong blockHash)
        {
            if (_bockHashToConfig.TryGetValue(blockHash, out var blockConfig))
            {
                return blockConfig;
            }

            throw new Exception("BlockHash not found:" + blockHash);
        }

        public int GetBlockConfigCount() { return _blockConfigList.Count; }
    }
}