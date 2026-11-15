using System;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// レシピ画面の操作の口（未知・未解放のレシピは送らずに良性でない拒否で返す）
    /// Port for recipe-screen operations; unknown or locked recipes return a non-benign rejection unsent
    /// </summary>
    /// <remarks>
    /// UITK のレシピビューアと Web の craft.execute（CraftExecuteActionHandler）の両方がこの口を呼ぶ。検査・送信・拒否ログは実装 1 本が持ち、Web は失敗コードへ写すだけ
    /// Both the UITK recipe viewer and the Web craft.execute (CraftExecuteActionHandler) call this port; the one implementation owns checking, sending and refusal logs, and Web only maps the failure code
    /// </remarks>
    public interface IRecipeCommands
    {
        UiCommandResult Craft(Guid recipeGuid);
    }
}
