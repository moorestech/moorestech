using System;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 開いた機械のレシピ選択コマンドの口
    /// Port selecting the open machine's recipe; the current one is accepted unsent, unsendable requests and server refusals return non-benign rejections
    /// </summary>
    public interface IMachineRecipeSelectCommand
    {
        UniTask<UiCommandResult> Select(Guid recipeGuid);
    }
}
