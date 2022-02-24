using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Blocks;
using Core.Block.Config;

namespace Core.Block.BlockFactory
{
    public class BlockFactory
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
            var type = _blockConfig.GetBlockConfig(blockId);
            if (_blockTypesDictionary.ContainsKey(type.Type))
            {
                return _blockTypesDictionary[type.Type].New(type, entityId);
            }

            throw new Exception("Block type not found :" + type.Type);
        }

        public IBlock Load(int blockId, int entityId, string state)
        {
            var type = _blockConfig.GetBlockConfig(blockId);
            if (_blockTypesDictionary.ContainsKey(type.Type))
            {
                return _blockTypesDictionary[type.Type].Load(type, entityId, state);
            }

            throw new Exception("Block type not found :" + type.Type);
        }

        public void RegisterTemplateIBlock(string key, IBlockTemplate block)
        {
            _blockTypesDictionary.Add(key, block);
        }
    }
}