using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
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
        private readonly BugReportBundleWriter _writer;
        private readonly BugReportCaptureSession _session;
        private readonly UIStateControl _uiStateControl;
        public string ActionType => "bug_report.submit";

        public BugReportSubmitActionHandler(BugReportBundleWriter writer, BugReportCaptureSession session, UIStateControl uiStateControl)
        {
            _writer = writer;
            _session = session;
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

            // 確保中・確保なし・二重送信の判定は確保セッションが持つ。ここで再実装すると判定の権威が2つになる
            // The capture session owns the pending / no-session / double-send decision; re-implementing it here would create a second authority
            var ticket = _session.TryBeginSubmit();
            if (!ticket.Allowed) return ActionResult.Fail(ticket.RefusedCode);

            var result = await _writer.WriteAsync(ticket.Data, description);

            // 書き出しで判明した欠損は確保状態へ戻す。戻さないと報告者は欠けたまま送ったことを知る機会が無い
            // Missing items found while writing go back into the capture state; otherwise the reporter never learns what was dropped
            _session.CompleteSubmit(ticket.Data, result.Ready, result.Missing);

            // READYの無い箱は運搬されない。成功として閉じるとユーザーは送ったつもりのまま何も届かない
            // A box without READY is never shipped; closing as a success leaves the user believing a lost report was sent
            if (!result.Ready)
            {
                Debug.LogError($"バグ報告を書き出せませんでした（運搬されません） {result.BundleDirectory}");
                return ActionResult.Fail("bundle_write_failed");
            }

            Debug.Log($"バグ報告を書き出しました {result.BundleDirectory} missing:{result.Missing.Count}");

            // 閉じは既存のWeb境界1本へ寄せる。閉じられなくても報告自体は書けているので成功として返す
            // Closing goes through the one existing web boundary; a refused close still leaves a written report, so the send succeeds
            var closed = RequestUiStateActionHandler.RequestState(_uiStateControl, nameof(UIStateEnum.GameScreen));
            if (!closed.Ok) Debug.LogWarning($"バグ報告の送信後にポーズメニューを閉じられませんでした error:{closed.Error}");
            return ActionResult.Success();
        }
    }
}
