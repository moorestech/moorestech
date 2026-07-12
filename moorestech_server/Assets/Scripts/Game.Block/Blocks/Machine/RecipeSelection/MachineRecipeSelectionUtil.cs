using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     レシピ選択の検証と、進行中ジョブの中断・返却の共通フロー（通常機械/クリーンルーム機械で共用）
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
    }
}
