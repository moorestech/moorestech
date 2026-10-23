using System;
using System.Collections.Generic;
using Core.Item.Interface;

namespace Core.Inventory
{
    // 順序と通知は呼出側が持ち、充填と余りの計算を共通化する
    // Callers own ordering and notifications; filling and remainder calculation are shared
    public static class InventoryInsertItem
    {
        internal static List<IItemStack> InsertItem(List<IItemStack> insertItemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option)
        {
            return InsertItem(insertItemStack, inventoryItems, itemStackFactory, option, null);
        }

        internal static List<IItemStack> InsertItem(List<IItemStack> insertItemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate)
        {
            var remainders = new List<IItemStack>();
            foreach (var item in insertItemStack)
            {
                var remainder = InsertItem(item, inventoryItems, itemStackFactory, option, onSlotUpdate);
                if (0 < remainder.Count) remainders.Add(remainder);
            }
            return remainders;
        }

        internal static IItemStack InsertItem(IItemStack insertItemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option)
        {
            return InsertItem(insertItemStack, inventoryItems, itemStackFactory, option, null);
        }

        internal static IItemStack InsertItem(IItemStack insertItemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate)
        {
            // 既存スタック優先と最後の同種枠からの近傍順を保つ
            // Preserve matching-stack priority and proximity order from the last matching slot
            var order = new List<int>();
            for (var i = 0; i < inventoryItems.Count; i++)
                if (0 < inventoryItems[i].Count && inventoryItems[i].Id == insertItemStack.Id) order.Add(i);
            if (option.AllowMultipleStacksPerItemOnInsert || order.Count == 0)
            {
                if (0 < order.Count)
                {
                    var origin = order[^1];
                    for (var offset = 1; offset < inventoryItems.Count; offset++)
                    {
                        if (0 <= origin - offset) order.Add(origin - offset);
                        if (origin + offset < inventoryItems.Count) order.Add(origin + offset);
                    }
                }
                for (var i = 0; i < inventoryItems.Count; i++) order.Add(i);
            }

            // 最終差分を従来の挿入順で通知し、仮挿入では通知しない
            // Notify final differences in the existing insertion order; simulations do not notify
            var before = inventoryItems.ToArray();
            var remainder = InsertInOrder(insertItemStack, inventoryItems, order);
            var notified = new HashSet<int>();
            foreach (var slot in order)
                if (notified.Add(slot) && !before[slot].Equals(inventoryItems[slot])) onSlotUpdate?.Invoke(slot);
            return remainder;
        }

        public static IItemStack InsertInOrder(IItemStack item, IList<IItemStack> slots, IReadOnlyList<int> slotOrder)
        {
            // 不変スタックを差し替え、属性・上限・long加算の規則はAddItemに委譲する
            // Replace immutable stacks and delegate metadata, limits and long arithmetic rules to AddItem
            var remainder = item;
            foreach (var slot in slotOrder)
            {
                if (remainder.Count == 0) break;
                // silent-ok: この枠へ入らない品は次の枠で試す / 最終余りを呼出側へ返して容量不足を伝える
                if (!slots[slot].IsAllowedToAddWithRemain(remainder)) continue;
                var result = slots[slot].AddItem(remainder);
                slots[slot] = result.ProcessResultItemStack;
                remainder = result.RemainderItemStack;
            }
            return remainder;
        }
    }
}
