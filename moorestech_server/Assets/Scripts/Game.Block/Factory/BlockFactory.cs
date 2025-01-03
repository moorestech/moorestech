using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;

namespace Game.Block.Factory
{
    public class BlockFactory : IBlockFactory
    {
        private readonly VanillaIBlockTemplates _vanillaIBlockTemplates;
        
        public BlockFactory(VanillaIBlockTemplates vanillaIBlockTemplates)
        {
            _vanillaIBlockTemplates = vanillaIBlockTemplates;
        }
        
        public IBlock Create(BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var dictionary = _vanillaIBlockTemplates.BlockTypesDictionary;
            
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (dictionary.TryGetValue(blockElement.BlockType, out var value))
                return value.New(blockElement, blockInstanceId, blockPositionInfo);
            
            throw new Exception("Block type not found :" + blockElement.BlockType);
        }
        
        public IBlock Load(Guid blockGuid, BlockInstanceId blockInstanceId, Dictionary<string, string> state, BlockPositionInfo blockPositionInfo)
        {
            var dictionary = _vanillaIBlockTemplates.BlockTypesDictionary;
            
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            if (dictionary.TryGetValue(blockElement.BlockType, out var value))
                return value.Load(state, blockElement, blockInstanceId, blockPositionInfo);
            
            throw new Exception("Block type not found :" + blockElement.BlockType);
        }
        
        public void RegisterTemplateIBlock(string key, IBlockTemplate block)
        {
            _vanillaIBlockTemplates.BlockTypesDictionary.Add(key, block);
        }
    }
}