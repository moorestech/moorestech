using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.Items
{
    /// <summary>
    /// 所持の判定と集計。recipe・research・inventory が共用する（Web shared/ownedCounts の写し）
    /// Owned predicate and tallies shared by recipe, research and inventory (port of Web shared/ownedCounts)
    /// </summary>
    public static class OwnedItemCounts
    {
        // 所持の唯一の判定。空に加え、wire の不正値（負の ID）も所持外にする（Web の共有契約）
        // The single owned predicate: excludes empties and invalid wire values (negative ids), per the Web contract
        public static bool IsOwnedSlot(SlotItem slot)
        {
            return !slot.IsEmpty && ItemMaster.EmptyItemId.AsPrimitive() < slot.ItemId.AsPrimitive();
        }

        // itemId 別の所持数。空スロット・0 個・負値は数えない
        // Owned count per itemId, skipping empties, zero and negative values
        public static IReadOnlyDictionary<ItemId, int> Build(IReadOnlyList<SlotItem> slots)
        {
            var owned = new Dictionary<ItemId, int>();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!IsOwnedSlot(slot)) continue;
                owned[slot.ItemId] = OwnedCountOf(owned, slot.ItemId) + slot.Count;
            }
            return owned;
        }

        // 同じアイテムが複数あるときは先頭の所持スロットだけがアンカーを持つ
        // When an item repeats, only its first owned slot carries the anchor
        public static IReadOnlyDictionary<ItemId, int> FirstSlotIndexByItemId(IReadOnlyList<SlotItem> slots)
        {
            var result = new Dictionary<ItemId, int>();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!IsOwnedSlot(slot) || result.ContainsKey(slot.ItemId)) continue;
                result.Add(slot.ItemId, i);
            }
            return result;
        }

        // 全必要アイテムを所持数が満たすか
        // Whether owned counts satisfy every requirement
        public static bool HasEnoughItems(IReadOnlyList<ItemAmount> requirements, IReadOnlyDictionary<ItemId, int> owned)
        {
            for (var i = 0; i < requirements.Count; i++)
            {
                if (OwnedCountOf(owned, requirements[i].ItemId) < requirements[i].Count) return false;
            }
            return true;
        }

        // 未登録は持っていない＝0 個。呼び出し側に同じ分岐を散らさない
        // Missing means not owned, i.e. zero; keeps the same branch out of callers
        public static int OwnedCountOf(IReadOnlyDictionary<ItemId, int> owned, ItemId itemId)
        {
            return owned.TryGetValue(itemId, out var count) ? count : 0;
        }
    }
}
