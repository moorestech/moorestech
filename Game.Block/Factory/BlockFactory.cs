using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Blocks;
using Core.Block.Config;
using Game.Block.Interface.Factory;

namespace Core.Block.BlockFactory
{
    public class BlockFactory : IBlockFactory
    {
        private readonly Dictionary<string, IBlockTemplate> _blockTypesDictionary;
        private readonly IBlockConfig _blockConfig;

        public BlockFactory(IBlockConfig blockConfig, VanillaIBlockTemplates vanillaIBlockTemplates)
        {
            _blockConfig = blockConfig;
            _blockTypesDictionary = vanillaIBlockTemplates.BlockTypesDictionary;
        }

        public IBlock Create(int blockId, int entityId)
        {
            var config = _blockConfig.GetBlockConfig(blockId);
            if (_blockTypesDictionary.ContainsKey(config.Type))
            {
                return _blockTypesDictionary[config.Type].New(config, entityId,config.BlockHash);
            }

            throw new Exception("Block type not found :" + config.Type);
        }

        public IBlock Load(ulong blockHash, int entityId, string state)
        {
            var config = _blockConfig.GetBlockConfig(blockHash);
            if (_blockTypesDictionary.ContainsKey(config.Type))
            {
                return _blockTypesDictionary[config.Type].Load(config, entityId,config.BlockHash, state);
            }

            throw new Exception("Block type not found :" + config.Type);
        }

        public void RegisterTemplateIBlock(string key, IBlockTemplate block)
        {
            _blockTypesDictionary.Add(key, block);
        }
    }
}