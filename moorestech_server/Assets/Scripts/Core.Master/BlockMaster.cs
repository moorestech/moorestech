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

        public readonly Blocks Blocks;

        private Dictionary<BlockId, BlockMasterElement> _blockElementTableById;
        private Dictionary<Guid, BlockId> _blockGuidToBlockId;
        private Dictionary<ItemId, BlockId> _itemIdToBlockId;
        private Dictionary<Guid, string> _blockGuidToDestructionCategory;
        private HashSet<(Guid, Guid)> _connectableShapePairs;

        public BlockMaster(JToken blockJToken)
        {
            Blocks = BlocksLoader.Load(blockJToken);
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

            // コネクタ形状の互換ペアを順序正規化して集合化する
            // Normalize pair order and collect connectable connector-shape pairs into a set
            _connectableShapePairs = new HashSet<(Guid, Guid)>();
            var connectableShapePairs = Blocks.ConnectorSettings?.ConnectableShapePairs;
            if (connectableShapePairs == null) return;
            foreach (var pair in connectableShapePairs)
            {
                _connectableShapePairs.Add(NormalizeShapePair(pair.Shape0, pair.Shape1));
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

        // コネクタ形状同士が接続可能かを返す。形状未設定はワイルドカード（制約なし）
        // Whether two connector shapes may connect; unset shapes are wildcard (no constraint)
        public bool CanConnectConnectorShapes(Guid? selfShapeGuid, Guid? targetShapeGuid)
        {
            if (selfShapeGuid == null || targetShapeGuid == null) return true;
            return _connectableShapePairs.Contains(NormalizeShapePair(selfShapeGuid.Value, targetShapeGuid.Value));
        }

        private static (Guid, Guid) NormalizeShapePair(Guid shapeA, Guid shapeB)
        {
            return shapeA.CompareTo(shapeB) <= 0 ? (shapeA, shapeB) : (shapeB, shapeA);
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