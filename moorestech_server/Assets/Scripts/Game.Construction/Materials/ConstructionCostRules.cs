using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Game.Construction
{
    /// <summary>
    /// 建設コストの充足判定と返却スタック生成。サーバーの実行とクライアントの先読みが同じ規則を見る唯一の家
    /// The single home for judging construction-cost affordability and building refund stacks, shared by the server's execution and the client's look-ahead
    /// </summary>
    public static class ConstructionCostRules
    {
        // 所持集計にこれから入る分を足して必要数を満たすか。撤去返却は消費より先に届くのでここへ渡す
        // Whether the tallied holdings plus what is about to arrive meet the requirement; a removal's refund lands before the consumption, so it comes in here
        public static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IReadOnlyDictionary<ItemId, int> heldByItem, IReadOnlyList<IItemStack> incomingItems)
        {
            if (itemCounts == null || itemCounts.Count == 0) return true;

            foreach (var (itemId, count) in itemCounts)
            {
                heldByItem.TryGetValue(itemId, out var available);
                foreach (var incomingItem in incomingItems)
                {
                    if (incomingItem.Id == itemId) available += incomingItem.Count;
                }
                if (available < count) return false;
            }

            return true;
        }

        // 所持スタック列版。集計してから正本へ委ねる
        // The item-stack version; tallies first and delegates to the canonical judgement
        public static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IEnumerable<IItemStack> inventoryItems)
        {
            if (itemCounts == null || itemCounts.Count == 0) return true;
            return HasRequiredItems(itemCounts, ConstructionMaterialAccounting.TallyHeld(inventoryItems), Array.Empty<IItemStack>());
        }

        // コスト全額分のスタックを作る
        // Creates the refund stacks matching the full cost definition
        public static List<IItemStack> CreateRefundItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts)
        {
            var result = new List<IItemStack>();
            if (itemCounts == null) return result;

            foreach (var (itemId, count) in itemCounts)
            {
                result.Add(ServerContext.ItemStackFactory.Create(itemId, count));
            }

            return result;
        }
    }
}
