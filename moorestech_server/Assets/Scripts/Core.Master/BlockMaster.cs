using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BlocksModule;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int))]
    public partial struct BlockId { }
    
    public class BlockMaster
    {
        public readonly Blocks Blocks;

        private readonly Dictionary<BlockId,BlockElement> _blockElementTableById; 
        private readonly Dictionary<Guid,BlockId> _blockGuidToBlockId;
        
        private readonly Dictionary<ItemId, BlockId> _itemIdToBlockId;
        
        public BlockMaster(JToken blockJToken, ItemMaster itemMaster)
        {
            // GUIDの順番にint型のItemIdを割り当てる
            Blocks = BlocksLoader.Load(blockJToken);
            var sortedBlockElements = Blocks.Data.ToList().OrderBy(x => x.BlockGuid).ToList();
            
            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            _blockElementTableById = new Dictionary<BlockId,BlockElement>();
            _blockGuidToBlockId = new Dictionary<Guid,BlockId>();
            for (var i = 1; i < sortedBlockElements.Count; i++)
            {
                _blockElementTableById.Add(new BlockId(i), sortedBlockElements[i]);
                _blockGuidToBlockId.Add(sortedBlockElements[i].BlockGuid, new BlockId(i));
            }
            
            // itemId to blockId
            _itemIdToBlockId = new Dictionary<ItemId, BlockId>();
            foreach (var blockElement in Blocks.Data)
            {
                var itemId = itemMaster.GetItemId(blockElement.ItemGuid);
                _itemIdToBlockId.Add(itemId, _blockGuidToBlockId[blockElement.BlockGuid]);
            }
        }
        
        public BlockElement GetBlockMaster(BlockId blockId)
        {
            if (!_blockElementTableById.TryGetValue(blockId, out var element))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemId:{blockId}");
            }
            return element;
        }
        
        public BlockElement GetBlockMaster(Guid blockGuid)
        {
            var blockId = GetBlockId(blockGuid);
            return GetBlockMaster(blockId);
        }
        
        public BlockId GetBlockId(Guid blockGuid)
        {
            if (!_blockGuidToBlockId.TryGetValue(blockGuid, out var blockId))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemGuid:{blockGuid}");
            }
            return blockId;
        }
        
        public bool IsBlock(ItemId itemId)
        {
            return _itemIdToBlockId.ContainsKey(itemId);
        }
        
        public BlockId ItemIdToBlockId(ItemId itemId)
        {
            return _itemIdToBlockId[itemId];
        }
    }
}