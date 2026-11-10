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
        UniTask<UiCommandResult> CompleteResearch(Guid researchGuid);
    }
}
