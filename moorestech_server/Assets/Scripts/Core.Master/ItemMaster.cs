using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ItemsModule;
using Mooresmaster.Model.ItemsModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct ItemId { }
    
    public class ItemMaster
    {
        public static readonly ItemId EmptyItemId = new(0);
        
        public readonly Items Items;
        
        private readonly Dictionary<ItemId,ItemMasterElement> _itemElementTableById; 
        private readonly Dictionary<Guid,ItemId> _itemGuidToItemId;
        
        public ItemMaster(JToken itemJToken)
        {
            // GUIDの順番にint型のItemIdを割り当てる
            Items = ItemsLoader.Load(itemJToken);
            
            // ソート優先度、GUIDの順番でソート
            var sortedItemElements = Items.Data.ToList().
                OrderBy(x => x.SortPriority ?? float.MaxValue).
                ThenBy(x => x.ItemGuid).
                ToList();
            
            _itemElementTableById = new Dictionary<ItemId,ItemMasterElement>();
            _itemGuidToItemId = new Dictionary<Guid,ItemId>();
            for (var i = 0; i < sortedItemElements.Count; i++)
            {
                var itemId = new ItemId(i+1); // アイテムID 0は空のアイテムとして予約しているので、1から始める
                _itemElementTableById.Add(itemId, sortedItemElements[i]);
                _itemGuidToItemId.Add(sortedItemElements[i].ItemGuid, itemId);
            }
        }
        
        public ItemMasterElement GetItemMaster(ItemId itemId)
        {
            if (!_itemElementTableById.TryGetValue(itemId, out var element))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemId:{itemId}");
            }
            return element;
        }
        
        public ItemMasterElement GetItemMaster(Guid itemGuid)
        {
            var itemId = GetItemId(itemGuid);
            return GetItemMaster(itemId);
        }
        
        public ItemId GetItemId(Guid itemGuid)
        {
            if (itemGuid == Guid.Empty)
            {
                return EmptyItemId;
            }
            
            var result = GetItemIdOrNull(itemGuid);
            if (result == null)
            {
                throw new InvalidOperationException($"ItemElement not found. ItemGuid:{itemGuid}");
            }
            return result.Value;
        }
        
        
        public ItemId? GetItemIdOrNull(Guid itemGuid)
        {
            if (!_itemGuidToItemId.TryGetValue(itemGuid, out var itemId))
            {
                return null;
            }
            return itemId;
        }
        
        public bool ExistItemId(Guid guid)
        {
            return _itemGuidToItemId.ContainsKey(guid);
        }
        
        public IEnumerable<ItemId> GetItemAllIds()
        {
            return _itemElementTableById.Keys;
        }
    }
}