using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator
{
    public static class ItemMasterUtil
    {
        public static bool Validate(Items items, out string errorLogs)
        {
            // ItemMasterは外部キー依存がないため、バリデーション成功を返す
            // ItemMaster has no external key dependencies, so return success
            errorLogs = "";
            return true;
        }

        public static void Initialize(
            Items items,
            out Dictionary<ItemId, ItemMasterElement> itemElementTableById,
            out Dictionary<Guid, ItemId> itemGuidToItemId)
        {
            // ソート優先度、GUIDの順番でソート
            // Sort by SortPriority, then by GUID
            var sortedItemElements = items.Data.ToList().
                OrderBy(x => x.SortPriority ?? float.MaxValue).
                ThenBy(x => x.ItemGuid).
                ToList();

            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            // Item ID 0 is reserved for empty item, so start from 1
            itemElementTableById = new Dictionary<ItemId, ItemMasterElement>();
            itemGuidToItemId = new Dictionary<Guid, ItemId>();
            for (var i = 0; i < sortedItemElements.Count; i++)
            {
                var itemId = new ItemId(i + 1);
                itemElementTableById.Add(itemId, sortedItemElements[i]);
                itemGuidToItemId.Add(sortedItemElements[i].ItemGuid, itemId);
            }
        }
    }
}
