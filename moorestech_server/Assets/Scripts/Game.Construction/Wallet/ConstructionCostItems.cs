using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;

namespace Game.Construction
{
    /// <summary>
    /// ブロック・車両のrequiredItemsを正準形(ItemId,個数)へ変換する唯一の家。サーバー・クライアント双方の財布が使う
    /// The single home converting block and train-car requiredItems into the canonical (ItemId,count) form used by the wallet on both sides
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

        // 車両用requiredItemsの変換。生成型に共通インタフェースが無いためオーバーロードで並べる
        // Conversion for train-car requiredItems; the generated types share no interface, so they stand as overloads
        public static (ItemId itemId, int count)[] ToItemCounts(TrainCarRequiredItemElement[] requiredItems)
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
