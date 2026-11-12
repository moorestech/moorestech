using System;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリーの画面から呼ぶ操作（A-8）
    /// Operations the research tree screen calls (A-8)
    /// </summary>
    public interface IResearchTreeCommands
    {
        void ToggleNodeSelection(Guid researchGuid);
        void CloseDetail();
        // 結果を返すだけで、トーストへは呼び手の View が渡す（A-262）
        // Only returns the result; the calling view hands it to the toaster (A-262)
        UniTask<UiCommandResult> CompleteResearch(Guid researchGuid);
    }
}
