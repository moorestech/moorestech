using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BlocksModule;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;
using UnityEngine;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct BlockId
    {
    }
    
    public class BlockMaster
    {
        public readonly Blocks Blocks;
        
        private readonly Dictionary<BlockId, BlockMasterElement> _blockElementTableById;
        private readonly Dictionary<Guid, BlockId> _blockGuidToBlockId;
        
        private readonly Dictionary<ItemId, BlockId> _itemIdToBlockId;
        
        public BlockMaster(JToken blockJToken, ItemMaster itemMaster)
        {
            // GUIDの順番にint型のItemIdを割り当てる
            Blocks = BlocksLoader.Load(blockJToken);
            var sortedBlockElements = Blocks.Data.ToList().OrderBy(x => x.BlockGuid).ToList();
            
            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            _blockElementTableById = new Dictionary<BlockId, BlockMasterElement>();
            _blockGuidToBlockId = new Dictionary<Guid, BlockId>();
            for (var i = 0; i < sortedBlockElements.Count; i++)
            {
                var blockId = new BlockId(i + 1); // アイテムID 0は空のアイテムとして予約しているので、1から始める
                _blockElementTableById.Add(blockId, sortedBlockElements[i]);
                _blockGuidToBlockId.Add(sortedBlockElements[i].BlockGuid, blockId);
            }
            
            // itemId to blockId
            _itemIdToBlockId = new Dictionary<ItemId, BlockId>();
            foreach (var blockElement in Blocks.Data)
            {
                var itemId = itemMaster.GetItemId(blockElement.ItemGuid);
                if (_itemIdToBlockId.TryGetValue(itemId, out var blockId))
                {
                    var existingBlockElement = GetBlockMaster(blockId);
                    throw new InvalidOperationException($"Duplicate itemId. Name1: {blockElement.Name}  Name2: {existingBlockElement.Name} ItemId:{blockElement.ItemGuid} BlockId:{blockElement.BlockGuid}");
                }
                else
                {
                    _itemIdToBlockId.Add(itemId, _blockGuidToBlockId[blockElement.BlockGuid]);
                }
            }
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