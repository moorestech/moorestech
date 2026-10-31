using System;
using System.Collections.Generic;
using Core.Item.Interface;

namespace Core.Inventory
{
    // 順序と通知は呼出側が持ち、充填と余りを共有する
    // Callers own ordering and notifications; filling and remainders are shared
    public static class InventoryInsertItem
    {
        internal static List<IItemStack> InsertItem(List<IItemStack> items, List<IItemStack> slots, OpenableInventoryItemDataStoreServiceOption option)
        {
            return InsertItem(items, slots, option, null);
        }

        internal static List<IItemStack> InsertItem(List<IItemStack> items, List<IItemStack> slots, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate)
        {
            var remainders = new List<IItemStack>();
            foreach (var item in items)
            {
                var remainder = InsertItem(item, slots, option, onSlotUpdate);
                if (0 < remainder.Count) remainders.Add(remainder);
            }
            return remainders;
        }

        internal static IItemStack InsertItem(IItemStack item, List<IItemStack> slots, OpenableInventoryItemDataStoreServiceOption option)
        {
            return InsertItem(item, slots, option, null);
        }

        internal static IItemStack InsertItem(IItemStack item, List<IItemStack> slots, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate)
        {
            return Fill(item, slots, InsertionOrder(item, slots, option), onSlotUpdate);
        }

        public static IItemStack InsertInOrder(IItemStack item, IList<IItemStack> slots, IReadOnlyList<int> slotOrder)
        {
            return Fill(item, slots, slotOrder, null);
        }

        // 複製した枠へ同種優先で全量を試し、確定と通知は呼出側へ任せる
        // Try all items on copied slots, preferring matching stacks; callers commit and notify
        public static bool TryFillAllPreferringExisting(IReadOnlyList<IItemStack> items, List<IItemStack> slots)
        {
            foreach (var item in items)
            {
                var order = new List<int>();
                for (var i = 0; i < slots.Count; i++)
                    if (0 < slots[i].Count && slots[i].Id == item.Id) order.Add(i);
                for (var i = 0; i < slots.Count; i++)
                    if (slots[i].Count == 0) order.Add(i);
                // silent-ok: 模擬失敗では原本を確定しない / 呼出側が容量待ちをUIへ通知する
                if (0 < InsertInOrder(item, slots, order).Count) return false;
            }
            return true;
        }

        private static IItemStack Fill(IItemStack item, IList<IItemStack> slots, IEnumerable<int> order, Action<int> onSlotUpdate)
        {
            // silent-ok: 空入力には充填も通知も不要 / 空の余りをそのまま呼出側へ返す
            if (item.Count == 0) return item;
            Dictionary<int, IItemStack> before = null;
            var remainder = item;
            foreach (var slot in order)
            {
                // silent-ok: 入らない品は次の枠で試す / 最終余りが容量不足を伝える
                if (!slots[slot].IsAllowedToAddWithRemain(remainder)) continue;
                var result = slots[slot].AddItem(remainder);
                // 値が同じなら個体を保ち、通知用の差分も作らない
                // Preserve the instance and avoid tracking when the value is unchanged
                if (!slots[slot].Equals(result.ProcessResultItemStack))
                {
                    if (onSlotUpdate != null)
                    {
                        before ??= new Dictionary<int, IItemStack>();
                        before.TryAdd(slot, slots[slot]);
                    }
                    slots[slot] = result.ProcessResultItemStack;
                }
                remainder = result.RemainderItemStack;
                // 充填完了後は次の候補を評価しない
                // Stop before evaluating another candidate once filling is complete
                if (remainder.Count == 0) break;
            }
            // 最初に変化した順で最終値を一度だけ通知する
            // Notify each final value once in the order of its first change
            if (before != null)
                foreach (var changed in before)
                    if (!changed.Value.Equals(slots[changed.Key])) onSlotUpdate(changed.Key);
            return remainder;
        }

        private static IEnumerable<int> InsertionOrder(IItemStack item, IList<IItemStack> slots, OpenableInventoryItemDataStoreServiceOption option)
        {
            var origin = -1;
            for (var i = 0; i < slots.Count; i++)
            {
                if (0 < slots[i].Count && slots[i].Id == item.Id)
                {
                    origin = i;
                    yield return i;
                }
            }
            // silent-ok: 単一スタック制約で追加枠を試さない / 余りを呼出側へ返す
            if (!option.AllowMultipleStacksPerItemOnInsert && 0 <= origin) yield break;
            // 一致枠の最後から近傍を試し、候補は必要な分だけ作る
            // Try neighbors of the last match, producing candidates only as needed
            if (0 <= origin)
                for (var offset = 1; offset < slots.Count; offset++)
                {
                    if (0 <= origin - offset) yield return origin - offset;
                    if (origin + offset < slots.Count) yield return origin + offset;
                }
            for (var i = 0; i < slots.Count; i++) yield return i;
        }
    }
}
