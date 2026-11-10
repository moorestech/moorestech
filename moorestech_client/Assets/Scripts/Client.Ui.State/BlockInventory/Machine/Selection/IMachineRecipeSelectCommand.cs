using System;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 開いた機械のレシピを選ぶコマンドの口。選択中と同じなら送らず受理、送れない要求とサーバーの拒否は非良性の拒否で返す
    /// Port for selecting the open machine's recipe; the current one is accepted without sending, unsendable requests and server refusals return non-benign rejections
    /// </summary>
    public interface IMachineRecipeSelectCommand
    {
        UniTask<UiCommandResult> Select(Guid recipeGuid);
    }
}
