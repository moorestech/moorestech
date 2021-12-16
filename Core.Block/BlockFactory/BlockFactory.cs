using System.Collections.Generic;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Config;

namespace Core.Block.BlockFactory
{
    public class BlockFactory
    {
        private readonly Dictionary<string,IBlockTemplate> _blockTypesDictionary;
        private readonly IBlockConfig _allMachineBlockConfig;

        public BlockFactory(IBlockConfig allMachineBlockConfig,VanillaIBlockTemplates vanillaIBlockTemplates)
        {
            _allMachineBlockConfig = allMachineBlockConfig;
            _blockTypesDictionary = vanillaIBlockTemplates.BlockTypesDictionary;
        }
        public IBlock Create(int blockId,int indId)
        {
            var type = _allMachineBlockConfig.GetBlockConfig(blockId);
            return _blockTypesDictionary[type.Type].New(type,indId);
        }
        public void RegisterTemplateIBlock(string key,IBlockTemplate block)
        {
            _blockTypesDictionary.Add(key,block);
        }
    }
}