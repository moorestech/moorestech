using System;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// レシピ画面の操作の口（未知・未解放のレシピは送らずに良性でない拒否で返す）
    /// Port for recipe-screen operations; unknown or locked recipes return a non-benign rejection unsent
    /// </summary>
    /// <remarks>
    /// 呼び出し元（レシピビューア）は解放済みの GUID しか渡さないが、検証は Web の CraftExecuteActionHandler と同じものを持つ。食い違えば拒否のトーストと LogWarning で見える
    /// Callers (the recipe viewer) pass only unlocked GUIDs, but validation mirrors the Web CraftExecuteActionHandler; a mismatch shows as a rejection toast and a LogWarning
    /// </remarks>
    public interface IRecipeCommands
    {
        UiCommandResult Craft(Guid recipeGuid);
    }
}
