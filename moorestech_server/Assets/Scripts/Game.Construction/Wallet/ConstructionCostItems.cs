using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Construction
{
    /// <summary>
    /// ブロックの建設コストを正準形(ItemId,個数)へ変換する。サーバー・クライアント双方の財布が使う
    /// Converts block construction costs into the canonical (ItemId,count) form used by the wallet on both sides
    /// </summary>
    public static class ConstructionCostItems
    {
        public static (ItemId itemId, int count)[] ToItemCounts(ConstructionRequiredItemElement[] requiredItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return Array.Empty<(ItemId, int)>();

            var result = new (ItemId, int)[requiredItems.Length];
            for (var i = 0; i < requiredItems.Length; i++)
            {
                result[i] = (MasterHolder.ItemMaster.GetItemId(requiredItems[i].ItemGuid), requiredItems[i].Count);
            }
            return result;
        }
    }
}
