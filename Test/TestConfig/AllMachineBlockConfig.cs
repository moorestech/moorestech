using System;
using System.Collections.Generic;
using System.IO;
using Core.Block.Config;

namespace Test.TestConfig
{
    public class AllMachineBlockConfig : IBlockConfig
    {
        private readonly Dictionary<int, BlockConfigData> _blockConfigDictionary;

        //TODO 機械レシピ用のテストコンフィグも作る
        public AllMachineBlockConfig()
        {
            var path = new TestConfigPath().GetPath("All Machine Block Config.json");
            _blockConfigDictionary = new BlockConfigJsonLoad(path).LoadJson();
        }

        public BlockConfigData GetBlockConfig(int id)
        {
            return _blockConfigDictionary[id];
        }
        
    }
}