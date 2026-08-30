using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     レシピ選択検証と中断・返却の共通処理
    ///     Shared validation and cancel-with-refund flow for recipe selection (vanilla and clean-room machines)
    /// </summary>
    internal static class MachineRecipeSelectionUtil
    {
        public static MachineRecipeSelectionResult ValidateSelection(VanillaMachineInputInventory inputInventory, MachineRecipeMasterElement recipe)
        {
            // レシピが自ブロックのものであること・アンロック済みであることをサーバー側で保証する
            // Server-side guarantees: the recipe belongs to this block and is unlocked
            if (MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid) != inputInventory.BlockId) return MachineRecipeSelectionResult.RecipeBlockMismatch;
            if (!inputInventory.IsRecipeUnlocked(recipe.MachineRecipeGuid)) return MachineRecipeSelectionResult.RecipeLocked;
            return MachineRecipeSelectionResult.Success;
        }

        // 進行中ジョブがあれば返却して中断する。アイテムが全量収容できなければfalse（変更自体を中止）
        // Cancel a running job with refund; returns false when items cannot be fully stored (abort the change)
        public static bool TryCancelRunningJobWithRefund(VanillaMachineInputInventory inputInventory, ProcessingMachineProcessState processingState, IOpenableInventory refundOverflowInventory)
        {
            var runningRecipe = processingState.CurrentRecipe;
            if (runningRecipe == null) return true;

            var refunds = MachineRecipeRefundUtil.CreateRefundStacks(runningRecipe);
            if (!MachineRecipeRefundUtil.CanRefundAllItems(inputInventory, refundOverflowInventory, refunds)) return false;

            MachineRecipeRefundUtil.ExecuteRefund(inputInventory, refundOverflowInventory, refunds, runningRecipe);
            processingState.CancelProcessing();
            return true;
        }

        // 新しい束縛の対象外になった入力スロットの未消費アイテムをプレイヤーへ返却する。戻せない分は元スロットへそのまま残す（消失させない）
        // Refund input slots that fell outside the new binding to the player; whatever does not fit stays in its original slot (never lost)
        public static void RefundUnboundInputItems(VanillaMachineInputInventory inputInventory, IOpenableInventory refundOverflowInventory)
        {
            for (var slot = 0; slot < inputInventory.InputSlot.Count; slot++)
            {
                var item = inputInventory.InputSlot[slot];
                if (item.Id == ItemMaster.EmptyItemId || item.Count == 0) continue;
                if (inputInventory.IsAllowedToPlace(slot, item)) continue;

                var remainder = refundOverflowInventory.InsertItem(item);
                inputInventory.SetItem(slot, remainder);
            }
        }
    }
}
