using System;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 開いた機械のレシピ選択コマンドの口
    /// Port selecting the open machine's recipe; the current one is accepted unsent, unsendable requests and server refusals are toasted as non-benign rejections by the implementation
    /// </summary>
    public interface IMachineRecipeSelectCommand
    {
        void Select(Guid recipeGuid);
    }
}
