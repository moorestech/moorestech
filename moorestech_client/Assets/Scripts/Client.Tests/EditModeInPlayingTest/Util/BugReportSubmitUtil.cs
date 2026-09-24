using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.PlaytestReceiver;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // ポーズメニュー確保→送信→増えた箱を取るまでの手順。確保の打ち切り時間とハンドラの引数はここ1箇所で追う
    // The capture-to-submit-to-new-bundle steps; the capture timeout and the handler's arguments are tracked in this one place
    public static class BugReportSubmitUtil
    {
        public static async UniTask<BugReportCaptureSession> OpenPauseMenuAndWaitCapture(IObjectResolver resolver)
        {
            var session = resolver.Resolve<BugReportCaptureSession>();
            resolver.Resolve<UIStateControl>().RequestTransition(UIStateEnum.PauseMenu);

            // サーバー確保の打ち切り上限は15秒なので、それより長く待って確定を見届ける
            // The server capture gives up after 15 seconds, so wait longer than that to see it settle
            for (var i = 0; i < 400 && session.Status.Value.Kind != BugReportCaptureStatus.Ready; i++) await UniTask.Delay(50);

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind, "ポーズメニューを開いても20秒以内に送信できる状態にならない");
            return session;
        }

        // 種別はwebuiのトグルが必ず載せる契約値で、欠けた要求は invalid_kind で拒否される
        // The kind is a contract value the webui toggle always sends; a request without it is refused as invalid_kind
        public static async UniTask<string> SubmitAndTakeNewBundle(IObjectResolver resolver, string description, PlaytestReportKind kind, IReadOnlyCollection<string> before)
        {
            var uploadRequester = new RecordingUploadRequester();
            var submitter = new BugReportSubmitter(
                resolver.Resolve<BugReportBundleWriter>(),
                resolver.Resolve<BugReportCaptureSession>(),
                resolver.Resolve<IPlaytestProgressSink>(),
                uploadRequester);
            var handler = new BugReportSubmitActionHandler(submitter, resolver.Resolve<PauseMenuStateService>());
            var kindText = PlaytestReportKindText.ToContractText(kind);
            var result = await handler.ExecuteAsync(new JObject { ["description"] = description, ["kind"] = kindText });
            Assert.IsTrue(result.Ok, result.Error);
            Assert.AreEqual(1, uploadRequester.RequestCount, "書けた箱は送信の押し場を必ず1回押す");

            // 起動時の退避が kind=crash の箱を同じoutboxへ足すため、増えた箱を数えるだけでは偽陽性になる。種別で絞る
            // The boot-time salvage adds a kind=crash box to the same outbox, so counting new boxes alone yields false positives; filter by kind
            var added = ExistingBundles().Except(before).Where(bundle => BundleKind(bundle) == kindText).ToList();
            Assert.AreEqual(1, added.Count, $"送信でoutboxに増えた kind={kind} の箱が1つではない");
            return added[0];
        }

        private static string BundleKind(string bundleDirectory)
        {
            var manifestPath = Path.Combine(bundleDirectory, BugReportBundleLayout.ManifestFileName);
            if (!File.Exists(manifestPath)) return null;
            return JObject.Parse(File.ReadAllText(manifestPath))["kind"]?.ToString();
        }

        public static List<string> ExistingBundles()
        {
            Directory.CreateDirectory(GameSystemPaths.BugReportOutboxDirectory);
            return Directory.GetDirectories(GameSystemPaths.BugReportOutboxDirectory).ToList();
        }
    }
}
