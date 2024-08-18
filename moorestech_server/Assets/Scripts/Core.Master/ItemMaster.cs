using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int))]
    public partial struct ItemId { }
    
    
    public class ItemMaster
    {
        private readonly Dictionary<ItemId,ItemElement> _itemElementTableById; 
        private readonly Dictionary<Guid,ItemId> _itemGuidToItemId;
        
        public static bool HasInstance => _instance != null;
        private static ItemMaster _instance;
        
        public static ItemElement GetItemMaster(ItemId itemId)
        {
            if (!HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is not loaded");
            }
            if (!_instance._itemElementTableById.TryGetValue(itemId, out var element))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemId:{itemId}");
            }
            return element;
        }
        
        public static ItemId GetItemId(Guid itemGuid)
        {
            if (!HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is not loaded");
            }
            if (!_instance._itemGuidToItemId.TryGetValue(itemGuid, out var itemId))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemGuid:{itemGuid}");
            }
            return itemId;
        }
        
        public static void Load()
        {
            if (HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is already loaded");
            }
            
            // GUIDの順番にint型のItemIdを割り当てる
            var itemElements = MasterHolder.Items.Data;
            var sortedItemElements = itemElements.ToList().OrderBy(x => x.ItemId).ToList();
            
            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            var itemElementTable = new Dictionary<ItemId,ItemElement>();
            var itemGuidToItemId = new Dictionary<Guid,ItemId>();
            for (var i = 1; i < sortedItemElements.Count; i++)
            {
                itemElementTable.Add(new ItemId(i), sortedItemElements[i]);
                itemGuidToItemId.Add(sortedItemElements[i].ItemId, new ItemId(i));
            }
            
            _instance = new ItemMaster(itemElementTable, itemGuidToItemId);
        }
        
        private ItemMaster(Dictionary<ItemId,ItemElement> itemElementTableById, Dictionary<Guid,ItemId> itemGuidToItemId)
        {
            _itemElementTableById = itemElementTableById;
            _itemGuidToItemId = itemGuidToItemId;
        }
    }
}