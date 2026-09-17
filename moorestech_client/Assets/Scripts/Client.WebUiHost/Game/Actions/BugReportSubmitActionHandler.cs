using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 説明文を受け取り、確保済みの記録と一緒に outbox へ書き、ポーズメニューを閉じる
    // Takes the description, writes it with the secured records into the outbox, then closes the pause menu
    public class BugReportSubmitActionHandler : IActionHandler
    {
        private readonly BugReportSubmitter _submitter;
        private readonly UIStateControl _uiStateControl;
        public string ActionType => "bug_report.submit";

        public BugReportSubmitActionHandler(BugReportSubmitter submitter, UIStateControl uiStateControl)
        {
            _submitter = submitter;
            _uiStateControl = uiStateControl;
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

            // 閉じは既存のWeb境界1本へ寄せる。閉じられなくても報告自体は書けているので成功として返す
            // Closing goes through the one existing web boundary; a refused close still leaves a written report, so the send succeeds
            var closed = RequestUiStateActionHandler.RequestState(_uiStateControl, nameof(UIStateEnum.GameScreen));
            if (!closed.Ok) Debug.LogWarning($"バグ報告の送信後にポーズメニューを閉じられませんでした error:{closed.Error}");
            return ActionResult.Success();
        }
    }
}
