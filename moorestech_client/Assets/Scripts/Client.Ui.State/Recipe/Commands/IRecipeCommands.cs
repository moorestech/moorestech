using System;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// レシピ画面の操作の口（未知・未解放のレシピは送らずに良性でない拒否で返す）
    /// Port for recipe-screen operations; unknown or locked recipes return a non-benign rejection unsent
    /// </summary>
    /// <remarks>
    /// UITK のレシピビューアがこの口を呼ぶ。検査・送信・拒否ログは実装 1 本（RecipeCommands）が持つ
    /// The UITK recipe viewer calls this port; the one implementation (RecipeCommands) owns checking, sending and refusal logs
    /// </remarks>
    public interface IRecipeCommands
    {
        UiCommandResult Craft(Guid recipeGuid);
    }
}
