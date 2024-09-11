using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;

namespace Game.Block.Factory
{
    public class BlockFactory : IBlockFactory
    {
        private readonly Dictionary<string, IBlockTemplate> _blockTypesDictionary;
        
        public BlockFactory(VanillaIBlockTemplates vanillaIBlockTemplates)
        {
            _blockTypesDictionary = vanillaIBlockTemplates.BlockTypesDictionary;
        }
        
        public IBlock Create(BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (_blockTypesDictionary.TryGetValue(blockElement.BlockType, out var value))
                return value.New(blockElement, blockInstanceId, blockPositionInfo);
            
            throw new Exception("Block type not found :" + blockElement.BlockType);
        }
        
        public IBlock Load(Guid blockGuid, BlockInstanceId blockInstanceId, string state, BlockPositionInfo blockPositionInfo)
        {
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            if (_blockTypesDictionary.TryGetValue(blockElement.BlockType, out var value))
                return value.Load(state, blockElement, blockInstanceId, blockPositionInfo);
            
            throw new Exception("Block type not found :" + blockElement.BlockType);
        }
        
        public void RegisterTemplateIBlock(string key, IBlockTemplate block)
        {
            _blockTypesDictionary.Add(key, block);
        }
    }
}