using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.Machine.Inventory
{
    // 束縛規則に基づく入力スロットへの仮想挿入シミュレーション（VanillaMachineInputInventoryの内部詰め物）。副作用を持たず結果のみ返す
    // Virtual insertion simulation into input slots under the binding rule (internal helper for VanillaMachineInputInventory); side-effect-free, returns only the result
    internal static class VanillaMachineInputSimulationUtil
    {
        public static (List<IItemStack> slots, List<int> touchedSlots, List<IItemStack> remainders) SimulateInsert(
            IReadOnlyList<IItemStack> currentSlots, IReadOnlyList<IItemStack> itemStacks, MachineInputSlotBinding slotBinding)
        {
            var slots = new List<IItemStack>(currentSlots);
            var touchedSlots = new List<int>();
            var remainders = new List<IItemStack>(itemStacks.Count);

            foreach (var stack in itemStacks)
            {
                var remaining = stack;
                foreach (var slot in slotBinding.ResolveBoundSlots(remaining.Id))
                {
                    if (remaining.Count == 0) break;
                    var result = slots[slot].AddItem(remaining);
                    slots[slot] = result.ProcessResultItemStack;
                    remaining = result.RemainderItemStack;
                    if (!touchedSlots.Contains(slot)) touchedSlots.Add(slot);
                }
                remainders.Add(remaining);
            }

            return (slots, touchedSlots, remainders);
        }
    }
}
