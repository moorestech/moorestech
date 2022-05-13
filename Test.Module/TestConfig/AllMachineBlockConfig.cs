using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;

namespace Test.Module.TestConfig
{
    public class AllMachineBlockConfig : IBlockConfig
    {
        private readonly Dictionary<int, BlockConfigData> _blockConfigDictionary;

        public AllMachineBlockConfig()
        {
            var path = new TestModuleConfigPath().GetPath("All Machine Block Config.json");
            _blockConfigDictionary = new BlockConfigJsonLoad().LoadFromJsons(path);
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
                new MachineBlockConfigParam(100, 100, 100),10);
        }

        public List<int> GetBlockIds()
        {
            return new List<int>(_blockConfigDictionary.Keys);
        }
    }
}