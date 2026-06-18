using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Core.Inventory
{
    public static class OpenableInventoryScanQuery
    {
        public static IReadOnlyList<int> CollectNonEmptySlotIndexes(IReadOnlyList<IItemStack> inventory)
        {
            var slots = new List<int>();

            // index無効の互換経路では必要な時だけslotを走査する
            // Scan slots only on demand for non-indexed compatibility paths
            for (var i = 0; i < inventory.Count; i++)
            {
                var itemStack = inventory[i];
                if (itemStack.Id != ItemMaster.EmptyItemId && itemStack.Count != 0) slots.Add(i);
            }

            return slots;
        }

        public static bool HasInsertableSlot(IReadOnlyList<IItemStack> inventory)
        {
            // 空slotか未満杯stackがあれば搬入可能性ありとみなす
            // Treat empty slots or partial stacks as potentially insertable
            for (var i = 0; i < inventory.Count; i++)
            {
                var itemStack = inventory[i];
                if (itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count == 0) return true;
                if (itemStack.Count < MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).MaxStack) return true;
            }

            return false;
        }
    }
}
