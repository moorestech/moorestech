using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.Machine.Inventory
{
    // スロット番号をサブインベントリ列から解決する共通ロジック（VanillaMachineBlockInventoryComponentの内部詰め物）
    // Common logic to resolve a slot number against a chain of sub-inventories (internal helper for VanillaMachineBlockInventoryComponent)
    internal static class VanillaMachineSlotResolutionUtil
    {
        public static (IVanillaMachineSubInventory subInventory, int localSlot) ResolveSlot(IReadOnlyList<IVanillaMachineSubInventory> subInventories, int slot)
        {
            // 負のスロットは境界で弾き、ローカル番号が負になるのを防ぐ
            // Reject negative slots at the boundary to avoid a negative local index
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "スロット番号が負の値です。 The slot number is negative.");
            }

            var requestedSlot = slot;
            foreach (var subInventory in subInventories)
            {
                if (slot < subInventory.Items.Count) return (subInventory, slot);
                slot -= subInventory.Items.Count;
            }

            throw new ArgumentOutOfRangeException(nameof(slot), requestedSlot, "スロット番号がインベントリサイズを超えています。 The slot number exceeds the inventory size.");
        }
    }
}
