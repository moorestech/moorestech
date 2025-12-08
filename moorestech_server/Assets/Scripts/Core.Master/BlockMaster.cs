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
        public readonly Blocks Blocks;

        private Dictionary<BlockId, BlockMasterElement> _blockElementTableById;
        private Dictionary<Guid, BlockId> _blockGuidToBlockId;
        private Dictionary<ItemId, BlockId> _itemIdToBlockId;

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