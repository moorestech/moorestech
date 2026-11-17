using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// ポーズ画面の操作。バグ報告の送信は結果をトーストへ渡したうえで、書きかけの扱いのため呼び手にも返す
    /// Pause-menu operations; a bug-report send hands its result to the toaster and also returns it so the caller can handle the draft
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
