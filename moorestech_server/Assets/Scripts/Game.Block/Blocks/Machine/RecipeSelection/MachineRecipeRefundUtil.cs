using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     加工中断時の消費済み材料の返却。アイテムは全量収容シミュレーション後に実行、液体はベストエフォート
    ///     Refunds consumed inputs on job cancel; items are simulated first, fluids are best-effort
    /// </summary>
    public static class MachineRecipeRefundUtil
    {
        // 返却対象のアイテム材料を生成する（isRemainは消費されていないため対象外）
        // Build the item stacks to refund (isRemain inputs are not consumed, so excluded)
        public static List<IItemStack> CreateRefundStacks(MachineRecipeMasterElement recipe)
        {
            var stacks = new List<IItemStack>();
            foreach (var input in recipe.InputItems)
            {
                if (input.IsRemain.HasValue && input.IsRemain.Value) continue;
                var itemId = MasterHolder.ItemMaster.GetItemId(input.ItemGuid);
                stacks.Add(ServerContext.ItemStackFactory.Create(itemId, input.Count));
            }
            return stacks;
        }

        // 入力インベントリ→溢れ先の順で全量収容できるか（アイテムのみ。液体は判定対象外）
        // Whether all item refunds fit into the input inventory then the overflow inventory (fluids excluded)
        public static bool CanRefundAllItems(VanillaMachineInputInventory input, IOpenableInventory overflow, List<IItemStack> refunds)
        {
            var inputRemainder = CopyMachineInput(input).InsertItem(refunds);
            var overflowRemainder = CopyOverflow(overflow).InsertItem(FilterNonEmpty(inputRemainder));
            return FilterNonEmpty(overflowRemainder).Count == 0;
        }

        public static void ExecuteRefund(VanillaMachineInputInventory input, IOpenableInventory overflow, List<IItemStack> refunds, MachineRecipeMasterElement recipe)
        {
            // シミュレーションと同じ順で実挿入する（入力→溢れ先）
            // Insert for real in the same order as the simulation (input first, then overflow)
            var remainder = input.InsertItem(refunds);
            overflow.InsertItem(FilterNonEmpty(remainder));
            RefundFluidsBestEffort(input, recipe);
        }

        // 機械入力インベントリの挿入規則（同一アイテム複数スタック禁止）を再現したコピー
        // Copy that mirrors the machine input insertion rule (no multiple stacks per item)
        private static OpenableInventoryItemDataStoreService CopyMachineInput(VanillaMachineInputInventory input)
        {
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var sim = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, input.InputSlot.Count, option);
            for (var i = 0; i < input.InputSlot.Count; i++) sim.SetItemWithoutEvent(i, input.InputSlot[i]);
            return sim;
        }

        private static OpenableInventoryItemDataStoreService CopyOverflow(IOpenableInventory overflow)
        {
            var sim = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, overflow.GetSlotSize());
            for (var i = 0; i < overflow.GetSlotSize(); i++) sim.SetItemWithoutEvent(i, overflow.GetItem(i));
            return sim;
        }

        private static List<IItemStack> FilterNonEmpty(List<IItemStack> stacks)
        {
            var result = new List<IItemStack>();
            foreach (var stack in stacks)
            {
                if (stack.Id != ItemMaster.EmptyItemId && stack.Count > 0) result.Add(stack);
            }
            return result;
        }

        // 液体は入力タンクへ戻せる分だけ戻し、収まらない分は消失させる（液体はインベントリで扱えないため）
        // Fluids go back to input tanks as far as capacity allows; the overflow is lost (no fluid inventory exists)
        private static void RefundFluidsBestEffort(VanillaMachineInputInventory input, MachineRecipeMasterElement recipe)
        {
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                // レシピ側のAmountはfloatだがFluidContainerはdoubleのため揃えてから計算する
                // The recipe's Amount is float while FluidContainer uses double, so widen before computing
                double remaining = inputFluid.Amount;
                foreach (var container in input.FluidInputSlot)
                {
                    if (remaining <= 0) break;
                    if (container.FluidId != fluidId && container.FluidId != FluidMaster.EmptyFluidId) continue;

                    var addable = Math.Min(remaining, container.Capacity - container.Amount);
                    if (addable <= 0) continue;
                    container.FluidId = fluidId;
                    container.Amount += addable;
                    remaining -= addable;
                }
            }
        }
    }
}
