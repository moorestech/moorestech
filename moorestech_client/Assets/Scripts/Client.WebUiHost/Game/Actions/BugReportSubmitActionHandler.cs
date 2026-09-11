using Client.Game.InGame.BugReport;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
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
        private readonly PauseMenuStateService _pauseMenuStateService;
        public string ActionType => "bug_report.submit";

        public BugReportSubmitActionHandler(BugReportBundleWriter writer, BugReportCaptureSession session, PauseMenuStateService pauseMenuStateService)
        {
            _writer = writer;
            _session = session;
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

            var data = _session.TakeCapturedData();
            if (data == null)
            {
                Debug.LogWarning("確保セッションが無いためバグ報告を送信しません（ポーズメニューを開き直してください）");
                return ActionResult.Fail("no_capture_session");
            }

            var result = await _writer.WriteAsync(data, description);
            Debug.Log($"バグ報告を書き出しました {result.BundleDirectory}");
            _pauseMenuStateService.RequestClose();
            return ActionResult.Success();
        }
    }
}
