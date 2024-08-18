using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;
using UnitGenerator;

namespace Core.Master
{
    public class ItemMaster
    {
        private readonly Dictionary<ItemId,ItemElement> _itemElementTable = new(); 
        
        public static bool HasInstance => _instance != null;
        private static ItemMaster _instance;
        
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
            for (var i = 1; i < sortedItemElements.Count; i++)
            {
                _instance._itemElementTable.Add(new ItemId(i), sortedItemElements[i]);
            }
            
            _instance = new ItemMaster(_instance._itemElementTable);
        }
        
        private ItemMaster(Dictionary<ItemId,ItemElement> itemElementTable)
        {
            _itemElementTable = itemElementTable;
        }
        
        public static ItemElement GetItemElement(ItemId itemId)
        {
            if (!HasInstance)
            {
                throw new InvalidOperationException("ItemMaster is not loaded");
            }
            if (!_instance._itemElementTable.TryGetValue(itemId, out var element))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemId:{itemId}");
            }
            return element;
        } 
    }
    
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int))]
    public partial struct ItemId { }
}