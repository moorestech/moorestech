using System;
using System.Collections.Generic;
using System.Text;
using Core.Master;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;

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

            // BP設定(UTF8 JSON)を対応コンポーネントへ適用
            // Generically apply blueprint settings (UTF8 JSON) to supporting components
            ApplyBlueprintSettings();

            return block;

            #region Internal

            void ApplyBlueprintSettings()
            {
                if (effectiveCreateParams.Length == 0) return;

                // キー一致するCreateParamを各設定コンポーネントへ適用
                // Apply key-matched create params to each IBlockBlueprintSettings component
                foreach (var component in block.ComponentManager.GetComponents<IBlockBlueprintSettings>())
                {
                    foreach (var param in effectiveCreateParams)
                    {
                        if (param.Key != component.BlueprintSettingsKey) continue;
                        component.ApplyBlueprintSettingsJson(Encoding.UTF8.GetString(param.Value));
                    }
                }
            }

            #endregion
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
