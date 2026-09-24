using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 説明文を受け取り、確保済みの記録と一緒に outbox へ書き、ポーズメニューのトップへ戻す（ADR 0069）
    // Takes the description, writes it with the secured records into the outbox, then returns to the pause-menu top (ADR 0069)
    public class BugReportSubmitActionHandler : IActionHandler
    {
        private readonly BugReportSubmitter _submitter;
        private readonly PauseMenuStateService _pauseMenuStateService;
        public string ActionType => "bug_report.submit";

        public BugReportSubmitActionHandler(BugReportSubmitter submitter, PauseMenuStateService pauseMenuStateService)
        {
            _submitter = submitter;
            _pauseMenuStateService = pauseMenuStateService;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var description = payload?["description"]?.ToString()?.Trim() ?? "";
            if (description.Length == 0)
            {
                Debug.LogWarning("バグ報告の説明文が空のため送信しません");
                return ActionResult.Fail("empty_description");
            }

            // 種別は webui のトグルが必ず載せる。載っていない・範囲外は壊れた要求として拒否する
            // The webui toggle always sends a kind; a missing or out-of-range value is a broken request
            // 文字列からの変換はこの payload パースだけで行い、以降は enum で持ち回す
            // Conversion from the string happens only at this payload parse; the enum is carried from here on
            var kindText = payload?["kind"]?.ToString() ?? "";
            if (!PlaytestReportKindText.TryParseSubmittableFromPauseMenu(kindText, out var kind))
            {
                Debug.LogWarning($"プレイ報告の種別が不正なため送信しません kind:{kindText}");
                return ActionResult.Fail("invalid_kind");
            }

            // 書き出し・送信記録・送信要求の手続きは通し検証と共有する1本に寄せる
            // The write, send-record and upload-request procedure lives in the one submitter shared with the smoke run
            var submitted = await _submitter.SubmitAsync(description, kind);
            if (!submitted.Submitted) return ActionResult.Fail(submitted.FailureCode);

            // 送れたらポーズは開いたままトップへ戻す。失敗時は画面を動かさず書きかけを残す
            // After a send the pause stays open and returns to the top; on failure the page stays so the draft survives
            _pauseMenuStateService.ShowPage(PauseMenuPage.Top);
            return ActionResult.Success();
        }
    }
}
