using System;
using System.Collections.Generic;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory
{
    public class BlockFactory : IBlockFactory
    {
        private readonly IBlockConfig _blockConfig;
        private readonly Dictionary<string, IBlockTemplate> _blockTypesDictionary;

        public BlockFactory(IBlockConfig blockConfig, VanillaIBlockTemplates vanillaIBlockTemplates)
        {
            _blockConfig = blockConfig;
            _blockTypesDictionary = vanillaIBlockTemplates.BlockTypesDictionary;
        }

        public IBlock Create(int blockId, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var config = _blockConfig.GetBlockConfig(blockId);
            if (_blockTypesDictionary.ContainsKey(config.Type))
                return _blockTypesDictionary[config.Type].New(config, entityId, config.BlockHash, blockPositionInfo);

            throw new Exception("Block type not found :" + config.Type);
        }

        public IBlock Load(long blockHash, int entityId, string state, BlockPositionInfo blockPositionInfo)
        {
            var config = _blockConfig.GetBlockConfig(blockHash);
            if (_blockTypesDictionary.ContainsKey(config.Type))
                return _blockTypesDictionary[config.Type].Load(config, entityId, config.BlockHash, state, blockPositionInfo);

            throw new Exception("Block type not found :" + config.Type);
        }

        public void RegisterTemplateIBlock(string key, IBlockTemplate block)
        {
            _blockTypesDictionary.Add(key, block);
        }
    }
}