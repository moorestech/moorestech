using System;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// レシピ画面の操作の口（未知・未解放のレシピは送らずに良性でない拒否をトーストへ渡す）
    /// Port for recipe-screen operations; unknown or locked recipes are refused unsent with a non-benign rejection handed to the toaster
    /// </summary>
    /// <remarks>
    /// UITK のレシピビューアがこの口を呼ぶ。検査・送信・拒否ログ・トーストは実装 1 本（RecipeCommands）が持つ
    /// The UITK recipe viewer calls this port; the one implementation (RecipeCommands) owns checking, sending, refusal logs and toasts
    /// </remarks>
    public interface IRecipeCommands
    {
        void Craft(Guid recipeGuid);
    }
}
