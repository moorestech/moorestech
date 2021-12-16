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
            //TODO なかった時はNullBlockConfigを返す
            //IDがなかったからインプット、アウトプットスロットが100のブロックを返す
            
            return new BlockConfigData(BlockConst.BlockConst.NullBlockId,
                "ID "+id+" is undefined",
                VanillaBlockType.Block,
                new NullBlockConfigParam());
        }

    }
}