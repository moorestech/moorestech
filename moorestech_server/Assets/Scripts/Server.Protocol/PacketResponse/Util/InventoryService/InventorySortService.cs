using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    /// <summary>
    /// インベントリを整理（同種アイテムをスタック結合し、ItemId 昇順 = SortPriority 順に詰め直す）するサービス
    /// Service that tidies an inventory by merging same-item stacks and re-packing them in ItemId (SortPriority) order
    /// </summary>
    public static class InventorySortService
    {
        public static void Sort(IOpenableInventory inventory, IReadOnlyCollection<int> excludeSlots)
        {
            // 整理対象スロットを決定（除外スロットを除く）
            // Determine target slots, excluding the given slots (e.g. hotbar).
            var targetSlots = Enumerable.Range(0, inventory.GetSlotSize())
                .Where(slot => !excludeSlots.Contains(slot))
                .ToList();

            // 対象スロットの非空アイテムを集めてスタック結合する
            // Collect non-empty items from the target slots and merge them into stacks.
            var mergedItems = new List<IItemStack>();
            foreach (var slot in targetSlots)
            {
                var item = inventory.GetItem(slot);
                if (item.Id == ItemMaster.EmptyItemId || item.Count == 0) continue;
                MergeItem(mergedItems, item);
            }

            // ItemId 昇順（= マスタの SortPriority 順）で安定ソートする
            // Stable-sort by ItemId ascending (equals master's SortPriority order).
            var sortedItems = mergedItems.OrderBy(item => item.Id.AsPrimitive()).ToList();

            // 対象スロットへ順に詰め直し、余ったスロットは空にする
            // Re-pack into the target slots in order; clear the remaining slots.
            var emptyItem = ServerContext.ItemStackFactory.CreatEmpty();
            for (var i = 0; i < targetSlots.Count; i++)
            {
                var slot = targetSlots[i];
                inventory.SetItem(slot, i < sortedItems.Count ? sortedItems[i] : emptyItem);
            }

            #region Internal

            void MergeItem(List<IItemStack> dest, IItemStack item)
            {
                // 既存スタックへ最大スタックまで詰め、あふれた分は次スタック／新スタックへ流す
                // Fill existing stacks up to max stack; overflow flows into the next or a new stack.
                var remaining = item;
                for (var i = 0; i < dest.Count && 0 < remaining.Count; i++)
                {
                    if (!dest[i].IsAllowedToAddWithRemain(remaining)) continue;
                    var result = dest[i].AddItem(remaining);
                    dest[i] = result.ProcessResultItemStack;
                    remaining = result.RemainderItemStack;
                }
                if (0 < remaining.Count) dest.Add(remaining);
            }

            #endregion
        }
    }
}
