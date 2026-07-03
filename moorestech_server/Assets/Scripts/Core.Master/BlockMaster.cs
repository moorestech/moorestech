using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
using Mooresmaster.Loader.BlocksModule;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct BlockId
    {
    }
    
    public class BlockMaster : IMasterValidator
    {
        // カテゴリー未設定ブロックの破壊カテゴリー。デフォルト同士は従来どおり複数選択可能
        // Destruction category for blocks not listed in any definition; defaults can still be multi-selected together
        public const string DefaultDestructionCategory = "default";
        public const float DefaultIdlePowerRate = 0.2f;

        public readonly Blocks Blocks;

        private Dictionary<BlockId, BlockMasterElement> _blockElementTableById;
        private Dictionary<Guid, BlockId> _blockGuidToBlockId;
        private Dictionary<ItemId, BlockId> _itemIdToBlockId;
        private Dictionary<Guid, string> _blockGuidToDestructionCategory;

        public BlockMaster(JToken blockJToken)
        {
            ApplyIdlePowerRateDefaults(blockJToken);
            Blocks = BlocksLoader.Load(blockJToken);
        }

        private static void ApplyIdlePowerRateDefaults(JToken blockJToken)
        {
            if (blockJToken["data"] is not JArray blockData) return;

            // 既存マスタの省略値をローダー前に補完する
            // Fill omitted values before the generated loader reads old masters
            foreach (var block in blockData.OfType<JObject>())
            {
                if (block["blockParam"] is not JObject blockParam) continue;

                // 電気消費ブロックは直下にidlePowerRateを持つ
                // Electric consumers carry idlePowerRate directly on blockParam
                var blockType = block.Value<string>("blockType");
                if (blockType is "ElectricMachine" or "ElectricMiner" or "ElectricPump")
                {
                    blockParam["idlePowerRate"] ??= DefaultIdlePowerRate;
                }

                // gearConsumption参照を持つ全ブロックへ省略値を補完する
                // Fill every gearConsumption reference so shared generated loaders can read it
                if (blockParam["gearConsumption"] is JObject gearConsumption)
                {
                    gearConsumption["idlePowerRate"] ??= DefaultIdlePowerRate;
                }
            }
        }

        public bool Validate(out string errorLogs)
        {
            return BlockMasterUtil.Validate(Blocks, out errorLogs);
        }

        public void Initialize()
        {
            BlockMasterUtil.Initialize(Blocks, out _blockElementTableById, out _blockGuidToBlockId, out _itemIdToBlockId, GetBlockMaster);

            // 破壊カテゴリ定義から blockGuid→カテゴリキー の逆引き表を構築する
            // Build a blockGuid→categoryKey reverse lookup from the destruction category definitions
            _blockGuidToDestructionCategory = new Dictionary<Guid, string>();
            foreach (var category in Blocks.BlockDestructionCategories)
            foreach (var target in category.TargetBlocks)
            {
                _blockGuidToDestructionCategory[target.BlockGuid] = category.CategoryKey;
            }
        }

        // ブロックの破壊カテゴリーを取得する。定義に無いブロックはdefault扱い
        // Get a block's destruction category; blocks absent from any definition are treated as default
        public string GetDestructionCategory(Guid blockGuid)
        {
            return _blockGuidToDestructionCategory.TryGetValue(blockGuid, out var category)
                ? category
                : DefaultDestructionCategory;
        }
        
        public BlockMasterElement GetBlockMaster(BlockId blockId)
        {
            if (!_blockElementTableById.TryGetValue(blockId, out var element))
            {
                throw new InvalidOperationException($"BlockElement not found. BlockId:{blockId}");
            }
            
            return element;
        }
        
        public BlockMasterElement GetBlockMaster(Guid blockGuid)
        {
            var blockId = GetBlockId(blockGuid);
            return GetBlockMaster(blockId);
        }
        
        public BlockMasterElement GetBlockMaster(ItemId itemId)
        {
            var blockId = GetBlockId(itemId);
            return GetBlockMaster(blockId);
        }
        
        public BlockId GetBlockId(Guid blockGuid)
        {
            if (!_blockGuidToBlockId.TryGetValue(blockGuid, out var blockId))
            {
                throw new InvalidOperationException($"BlockElement not found. BlockGuid:{blockGuid}");
            }

            return blockId;
        }

        public BlockId? GetBlockIdOrNull(Guid blockGuid)
        {
            if (!_blockGuidToBlockId.TryGetValue(blockGuid, out var blockId))
            {
                return null;
            }

            return blockId;
        }
        
        public List<BlockId> GetBlockAllIds()
        {
            return _blockElementTableById.Keys.ToList();
        }
        
        public bool IsBlock(ItemId itemId)
        {
            return _itemIdToBlockId.ContainsKey(itemId);
        }
        
        public BlockId GetBlockId(ItemId itemId)
        {
            return _itemIdToBlockId[itemId];
        }
        
        public ItemId GetItemId(BlockId blockId)
        {
            foreach (var pair in _itemIdToBlockId)
            {
                if (pair.Value == blockId)
                {
                    return pair.Key;
                }
            }
            
            throw new InvalidOperationException($"ItemElement not found. BlockId:{blockId}");
        }
    }
}
