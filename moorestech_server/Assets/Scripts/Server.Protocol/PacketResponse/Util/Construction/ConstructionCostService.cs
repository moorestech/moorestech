using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 建設コスト(requiredItems)の検証・消費・返却スタック生成を行う
    /// Validates, consumes, and creates refund stacks for construction costs (requiredItems)
    /// </summary>
    public static class ConstructionCostService
    {
        // 所持判定は正本 ConstructionMaterialAccounting.HasEnough に委ねる
        // Delegate the affordability check to the canonical ConstructionMaterialAccounting.HasEnough
        public static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IReadOnlyList<IItemStack> inventoryItems)
        {
            return ConstructionMaterialAccounting.HasEnough(ConnectToolMaterialConsumer.ToMaterials(itemCounts), inventoryItems, null);
        }

        public static void ConsumeRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IOpenableInventory inventory)
        {
            if (itemCounts == null || itemCounts.Count == 0) return;

            // 先頭スロットから順に減算する共通処理（電線消費と同一実装）を再利用する
            // Reuse the shared first-slot-onward consumption logic used by wire consumption
            foreach (var (itemId, count) in itemCounts)
            {
                ConnectToolMaterialConsumer.ConsumeItem(inventory, itemId, count);
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
