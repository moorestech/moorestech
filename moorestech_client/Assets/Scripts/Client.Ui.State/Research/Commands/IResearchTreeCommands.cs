using System;

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
        // 結果は実装がトーストへ渡す（呼び手は投げるだけ）
        // The implementation hands the result to the toaster (callers only fire it)
        void CompleteResearch(Guid researchGuid);
    }
}
