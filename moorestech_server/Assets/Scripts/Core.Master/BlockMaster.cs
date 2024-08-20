using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int))]
    public partial struct BlockId { }
    
    public class BlockMaster
    {
        private readonly Dictionary<BlockId,BlockElement> _blockElementTableById; 
        private readonly Dictionary<Guid,BlockId> _blockGuidToBlockId;
        
        public static bool HasInstance => _instance != null;
        private static BlockMaster _instance;
        
        public static BlockElement GetItemMaster(BlockId blockId)
        {
            if (!HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is not loaded");
            }
            if (!_instance._blockElementTableById.TryGetValue(blockId, out var element))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemId:{blockId}");
            }
            return element;
        }
        
        public static BlockId GetItemId(Guid blockGuid)
        {
            if (!HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is not loaded");
            }
            if (!_instance._blockGuidToBlockId.TryGetValue(blockGuid, out var blockId))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemGuid:{blockGuid}");
            }
            return blockId;
        }
        
        public static void Load()
        {
            if (HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is already loaded");
            }
            
            // GUIDの順番にint型のItemIdを割り当てる
            var blockElements = MasterHolder.Blocks.Data;
            var sortedItemElements = blockElements.ToList().OrderBy(x => x.ItemId).ToList();
            
            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            var blockElementTable = new Dictionary<BlockId,BlockElement>();
            var blockGuidToBlockId = new Dictionary<Guid,BlockId>();
            for (var i = 1; i < sortedItemElements.Count; i++)
            {
                blockElementTable.Add(new BlockId(i), sortedItemElements[i]);
                blockGuidToBlockId.Add(sortedItemElements[i].ItemId, new BlockId(i));
            }
            
            _instance = new BlockMaster(blockElementTable, blockGuidToBlockId);
        }
        
        private BlockMaster(Dictionary<BlockId,BlockElement> blockElementTableById, Dictionary<Guid,BlockId> blockGuidToBlockId)
        {
            _blockElementTableById = blockElementTableById;
            _blockGuidToBlockId = blockGuidToBlockId;
        }
    }
}