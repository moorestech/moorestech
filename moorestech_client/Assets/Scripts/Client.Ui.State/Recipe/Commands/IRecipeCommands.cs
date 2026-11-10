using System;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// レシピ画面の操作の口（未知・未解放のレシピは送らずに良性でない拒否で返す）
    /// Port for recipe-screen operations; unknown or locked recipes return a non-benign rejection unsent
    /// </summary>
    public interface IRecipeCommands
    {
        UiCommandResult Craft(Guid recipeGuid);
    }
}
