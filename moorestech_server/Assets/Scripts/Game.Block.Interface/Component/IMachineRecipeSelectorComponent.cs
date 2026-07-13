using System;
using Core.Inventory;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     機械のレシピ選択を受け付けるコンポーネント。未選択の機械は加工しない
    ///     Accepts machine recipe selection; machines without a selection never process
    /// </summary>
    public interface IMachineRecipeSelectorComponent : IBlockComponent
    {
        Guid SelectedRecipeGuid { get; }

        // 加工中の変更はジョブを中断し消費済み材料を返却する。入力に収まらない分はrefundOverflowInventoryへ
        // Changing mid-processing cancels the job and refunds consumed inputs; overflow goes to refundOverflowInventory
        MachineRecipeSelectionResult SetSelectedRecipe(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory);
        MachineRecipeSelectionResult ClearSelectedRecipe(IOpenableInventory refundOverflowInventory);
    }

    public enum MachineRecipeSelectionResult
    {
        Success,
        RecipeBlockMismatch,
        RecipeLocked,
        RefundFailed,
    }
}
