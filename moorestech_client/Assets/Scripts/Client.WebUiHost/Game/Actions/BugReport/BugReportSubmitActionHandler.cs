using Client.Game.InGame.BugReport.Playtest;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // JSON を解いてバグ報告の型付きコマンドへ渡す。送信とトップ戻りの判断はコマンドが持つ（ADR 0069）
    // Parses the JSON and calls the typed bug-report command, which owns sending and returning to the top (ADR 0069)
    public class BugReportSubmitActionHandler : IActionHandler
    {
        private readonly IPauseMenuCommands _commands;
        public string ActionType => "bug_report.submit";

        public BugReportSubmitActionHandler(IPauseMenuCommands commands)
        {
            _commands = commands;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var description = payload?["description"]?.ToString() ?? "";

            // 種別は webui のトグルが必ず載せる。文字列からの変換はこの payload パースだけで行う
            // The webui toggle always sends a kind; the string is converted only at this payload parse
            var kindText = payload?["kind"]?.ToString() ?? "";
            if (!PlaytestReportKindText.TryParseSubmittableFromPauseMenu(kindText, out var kind))
            {
                Debug.LogWarning($"プレイ報告の種別が不正なため送信しません kind:{kindText}");
                return ActionResult.Fail("invalid_kind");
            }

            return ToActionResult(await _commands.SubmitBugReport(description, kind));
        }

        private static ActionResult ToActionResult(UiCommandResult result)
        {
            return result switch
            {
                UiCommandAccepted => ActionResult.Success(),
                UiCommandRejected rejected => ActionResult.Fail(rejected.ToWireCode()),
                _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
            };
        }
    }
}
