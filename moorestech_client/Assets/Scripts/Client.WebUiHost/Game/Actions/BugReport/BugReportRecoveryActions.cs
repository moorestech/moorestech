using Client.Ui.State.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    // 送信できない報告の再送。結果は配送状況のtopicで届く
    // Resend of undeliverable reports; the outcome arrives through the delivery topic
    public class BugReportResendFailedActionHandler : IActionHandler
    {
        private readonly IPauseMenuCommands _commands;
        public string ActionType => "bug_report.resend_failed";
        public BugReportResendFailedActionHandler(IPauseMenuCommands commands) { _commands = commands; }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _commands.ResendFailedReports();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    // 未送信報告のzip書き出し。受理だけ返し進行はtopicで届く
    // Zip export of unsent reports; only acceptance returns, progress arrives on the topic
    public class BugReportExportUnsentActionHandler : IActionHandler
    {
        private readonly IPauseMenuCommands _commands;
        public string ActionType => "bug_report.export_unsent";
        public BugReportExportUnsentActionHandler(IPauseMenuCommands commands) { _commands = commands; }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _commands.ExportUnsentReports();
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
