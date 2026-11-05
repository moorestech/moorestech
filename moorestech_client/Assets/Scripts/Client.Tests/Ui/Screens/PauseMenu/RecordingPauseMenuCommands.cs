using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    public enum PauseMenuCommandKind
    {
        Save,
        SaveAndQuit,
        ShowPage,
        SubmitBugReport,
        ResendFailedReports,
        ExportUnsentReports,
    }

    // 発行されたポーズのコマンド 1 件。ページと報告の中身は該当するコマンドだけが持つ
    // One issued pause-menu command; the page and report payload are set only by the commands that carry them
    public readonly struct PauseMenuCommandRecord
    {
        public readonly PauseMenuCommandKind Kind;
        public readonly PauseMenuPage Page;
        public readonly string Description;
        public readonly PlaytestReportKind ReportKind;

        public PauseMenuCommandRecord(PauseMenuCommandKind kind, PauseMenuPage page, string description, PlaytestReportKind reportKind)
        {
            Kind = kind;
            Page = page;
            Description = description;
            ReportKind = reportKind;
        }
    }

    /// <summary>
    /// ポーズのコマンドの記録係。送信の結果はテストが CompleteSubmit で決めるまで返らない
    /// Recorder of pause-menu commands; a submit does not return until the test decides its result with CompleteSubmit
    /// </summary>
    public sealed class RecordingPauseMenuCommands : CommandRecorder<PauseMenuCommandRecord>, IPauseMenuCommands
    {
        private UniTaskCompletionSource<UiCommandResult> _pendingSubmit = new();

        public void Save() => RecordKind(PauseMenuCommandKind.Save);
        public void SaveAndQuit() => RecordKind(PauseMenuCommandKind.SaveAndQuit);
        public void ShowPage(PauseMenuPage page) => Record(new PauseMenuCommandRecord(PauseMenuCommandKind.ShowPage, page, null, PlaytestReportKind.Bug));
        public void ResendFailedReports() => RecordKind(PauseMenuCommandKind.ResendFailedReports);
        public void ExportUnsentReports() => RecordKind(PauseMenuCommandKind.ExportUnsentReports);

        public UniTask<UiCommandResult> SubmitBugReport(string description, PlaytestReportKind kind)
        {
            Record(new PauseMenuCommandRecord(PauseMenuCommandKind.SubmitBugReport, PauseMenuPage.Top, description, kind));
            return _pendingSubmit.Task;
        }

        // 待っている送信に結果を返し、次の送信は新しい待ちにする
        // Resolve the waiting submit and make the next submit wait afresh
        public void CompleteSubmit(UiCommandResult result)
        {
            var pending = _pendingSubmit;
            _pendingSubmit = new UniTaskCompletionSource<UiCommandResult>();
            pending.TrySetResult(result);
        }

        private void RecordKind(PauseMenuCommandKind kind)
        {
            Record(new PauseMenuCommandRecord(kind, PauseMenuPage.Top, null, PlaytestReportKind.Bug));
        }
    }
}
