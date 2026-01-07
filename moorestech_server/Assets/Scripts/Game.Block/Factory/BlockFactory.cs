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
        
        public IBlock Create(BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var dictionary = _vanillaIBlockTemplates.BlockTypesDictionary;
            
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (!dictionary.TryGetValue(blockElement.BlockType, out var value)) throw new Exception("Block type not found :" + blockElement.BlockType);
            
            var effectiveCreateParams = createParams ?? Array.Empty<BlockCreateParam>();
            var block = value.New(blockElement, blockInstanceId, blockPositionInfo, effectiveCreateParams);
            
            return block;
        }
        
        public IBlock Load(Guid blockGuid, BlockInstanceId blockInstanceId, Dictionary<string, string> state, BlockPositionInfo blockPositionInfo)
        {
            var dictionary = _vanillaIBlockTemplates.BlockTypesDictionary;
            
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            try
            {
                if (dictionary.TryGetValue(blockElement.BlockType, out var value))
                    return value.Load(state, blockElement, blockInstanceId, blockPositionInfo);
            }
            catch (Exception e)
            {
                throw new Exception($"Block Load Error name:{blockElement.Name} guid:{blockElement.BlockGuid} \n Message:{e.Message} \nStackTrace:{e.StackTrace}", e);
            }
            
            throw new Exception("Block type not found :" + blockElement.BlockType);
        }
        
        public void RegisterTemplateIBlock(string key, IBlockTemplate block)
        {
            _vanillaIBlockTemplates.BlockTypesDictionary.Add(key, block);
        }
    }
}
