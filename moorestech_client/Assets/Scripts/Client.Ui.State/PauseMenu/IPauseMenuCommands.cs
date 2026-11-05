using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// ポーズ画面の操作。Web のアダプタと GameUi の View が同じ口を呼ぶ
    /// Pause-menu operations; the Web adapters and the GameUi views call the same entry points
    /// </summary>
    public interface IPauseMenuCommands
    {
        void Save();
        void SaveAndQuit();
        void ShowPage(PauseMenuPage page);
        UniTask<UiCommandResult> SubmitBugReport(string description, PlaytestReportKind kind);
        void ResendFailedReports();
        void ExportUnsentReports();
    }
}
