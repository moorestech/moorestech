using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;

namespace Test.Module.TestConfig
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
            if (_blockConfigDictionary.ContainsKey(id))
            {
                return _blockConfigDictionary[id];   
            }
            //IDがなかったからインプット、アウトプットスロットが100のブロックを返す
            return new BlockConfigData(id,
                "Generated Block from AllMachineBlockConfig.cs",
                "Machine",
                new MachineBlockConfigParam(100,100,100));
        }
        
    }
}