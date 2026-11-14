using System;
using Client.Ui.State.Commands;
using Game.UnlockState;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// クラフトのレシピを送ってよいかの唯一の判定。解放表に無い→RecipeInvalid、未解放→RecipeLocked、送ってよい→null（UITK の RecipeCommands と Web の CraftActions が呼ぶ。DC-23 A）
    /// The single decision on whether a craft recipe may be sent: absent from the unlock table → RecipeInvalid, locked → RecipeLocked, sendable → null (called by the UITK RecipeCommands and the Web CraftActions; DC-23 A)
    /// </summary>
    public static class CraftRecipeUnlockCheck
    {
        public static UiCommandRejectReason? Check(IGameUnlockStateData unlockState, Guid recipeGuid)
        {
            if (!unlockState.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var info)) return UiCommandRejectReason.RecipeInvalid;
            if (!info.IsUnlocked) return UiCommandRejectReason.RecipeLocked;
            return null;
        }
    }
}
