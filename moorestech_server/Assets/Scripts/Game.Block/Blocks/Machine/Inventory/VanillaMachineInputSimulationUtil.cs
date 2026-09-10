using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Context;

namespace Game.Block.Blocks.Machine.Inventory
{
    // 束縛規則に基づく入力スロットへの仮想挿入シミュレーション（VanillaMachineInputInventoryの内部詰め物）。副作用を持たず結果のみ返す
    // Virtual insertion simulation into input slots under the binding rule (internal helper for VanillaMachineInputInventory); side-effect-free, returns only the result
    internal static class VanillaMachineInputSimulationUtil
    {
        public static (List<IItemStack> slots, List<int> touchedSlots, List<IItemStack> remainders) SimulateInsert(
            IReadOnlyList<IItemStack> currentSlots, IReadOnlyList<IItemStack> itemStacks, MachineInputSlotBinding slotBinding)
        {
            // レシピ未選択の機械は何も受け入れないので、複製も作らず入口で全量突き返す
            // An unselected machine accepts nothing, so bounce the whole input at the entrance without even copying the slots
            if (!slotBinding.IsBound()) return (new List<IItemStack>(currentSlots), new List<int>(), new List<IItemStack>(itemStacks));

            var slots = new List<IItemStack>(currentSlots);
            var touchedSlots = new List<int>();
            var remainders = new List<IItemStack>(itemStacks.Count);

            foreach (var stack in itemStacks)
            {
                // 第一パスで各枠をレシピ必要数まで満たしてから、第二パスで残りをスタック上限まで詰める。
                // 貪欲に先頭から詰めると、同一素材を複数枠が要求するレシピで先頭枠だけが太り加工が始まらない
                // Fill every slot up to its recipe requirement first, then top up to the stack limit.
                // Greedy front-filling would fatten only the first slot when several slots want the same item, and processing never starts
                var remaining = FillToRequirement(stack);
                remaining = FillToStackLimit(remaining);
                remainders.Add(remaining);
            }

            return (slots, touchedSlots, remainders);

            #region Internal

            IItemStack FillToRequirement(IItemStack remaining)
            {
                foreach (var slot in slotBinding.ResolveBoundSlots(remaining.Id))
                {
                    if (remaining.Count == 0) break;

                    var room = slotBinding.RequiredCountAt(slot) - slots[slot].Count;
                    if (room <= 0) continue;

                    var portion = ServerContext.ItemStackFactory.Create(remaining.Id, Math.Min(remaining.Count, room));
                    var result = slots[slot].AddItem(portion);
                    var added = portion.Count - result.RemainderItemStack.Count;
                    if (added == 0) continue;

                    slots[slot] = result.ProcessResultItemStack;
                    remaining = remaining.SubItem(added);
                    if (!touchedSlots.Contains(slot)) touchedSlots.Add(slot);
                }
                return remaining;
            }

            IItemStack FillToStackLimit(IItemStack remaining)
            {
                foreach (var slot in slotBinding.ResolveBoundSlots(remaining.Id))
                {
                    if (remaining.Count == 0) break;

                    var result = slots[slot].AddItem(remaining);
                    if (result.RemainderItemStack.Count == remaining.Count) continue;

                    slots[slot] = result.ProcessResultItemStack;
                    remaining = result.RemainderItemStack;
                    if (!touchedSlots.Contains(slot)) touchedSlots.Add(slot);
                }
                return remaining;
            }

            #endregion
        }
    }
}
