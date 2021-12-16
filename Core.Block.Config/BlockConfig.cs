using System.Collections.Generic;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.Param;

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
            //TODO なかった時の処理を考える
            //IDがなかったからインプット、アウトプットスロットが100のブロックを返す
            return new BlockConfigData(id,
                "Generated Block from AllMachineBlockConfig.cs",
                "Machine",
                new MachineBlockConfigParam(100,100,100));
        }

    }
}