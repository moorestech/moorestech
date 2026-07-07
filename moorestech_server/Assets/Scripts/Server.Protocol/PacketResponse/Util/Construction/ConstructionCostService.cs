using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 建設コスト(requiredItems)の検証・消費・返却スタック生成を行う
    /// Validates, consumes, and creates refund stacks for construction costs (requiredItems)
    /// </summary>
    public static class ConstructionCostService
    {
        // ブロック用requiredItemsを正準形(ItemId,個数)へ変換する。電線予約リストと同型
        // Convert block requiredItems to the canonical (ItemId,count) form, shared with wire reservations
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

        // 車両用requiredItemsの変換。生成型が別なだけで内容は同じ
        // Conversion for train-car requiredItems; a distinct generated type with the same shape
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

        public static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IReadOnlyList<IItemStack> inventoryItems)
        {
            if (itemCounts == null || itemCounts.Count == 0) return true;

            // 全スロットの所持数を合算
            // Sum held counts across all inventory slots per material
            foreach (var (itemId, count) in itemCounts)
            {
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                if (total < count) return false;
            }

            return true;
        }

        public static void ConsumeRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IOpenableInventory inventory)
        {
            if (itemCounts == null || itemCounts.Count == 0) return;

            // 先頭スロットから順に減算する共通処理（電線消費と同一実装）を再利用する
            // Reuse the shared first-slot-onward consumption logic used by wire consumption
            foreach (var (itemId, count) in itemCounts)
            {
                ElectricWireSystemUtil.ConsumeItem(inventory, itemId, count);
            }
        }

        public static List<IItemStack> CreateRefundItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts)
        {
            var result = new List<IItemStack>();
            if (itemCounts == null) return result;

            // コスト全額分のスタック生成
            // Create refund stacks matching the full cost definition
            foreach (var (itemId, count) in itemCounts)
            {
                result.Add(ServerContext.ItemStackFactory.Create(itemId, count));
            }

            return result;
        }
    }
}
