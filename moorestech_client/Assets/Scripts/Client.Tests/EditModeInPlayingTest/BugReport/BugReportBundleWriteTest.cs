using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BugReport
{
    // 確保→送信→outbox を通しで固定する。バンドルの形は plan C の prepare-run.sh が読む契約そのもの
    // Pins capture to submit to outbox end to end; the bundle shape is the very contract plan C's prepare-run.sh reads
    [Category("CiShardClientPlay3")]
    public class BugReportBundleWriteTest
    {
        [UnityTest]
        public IEnumerator 常時記録が動いていればスナップショット入りのバンドルがoutboxに揃う()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();

            UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await EditModeInPlayingTestUtil.LoadMainGame();
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;

                // テスト起動は常時記録オフなので、リングだけ明示的に開始する
                // Test boots disable always-on capture, so start the ring explicitly
                ServerContext.GetService<WorldSnapshotRing>().Start(null, null, null);
                await UniTask.Delay(1000);

                var before = BugReportSubmitUtil.ExistingBundles();
                var session = await BugReportSubmitUtil.OpenPauseMenuAndWaitCapture(resolver);
                Assert.IsFalse(session.Status.Value.Missing.Contains("serverSnapshot"), "サーバースナップショットの確保に失敗した");

                // 実際の送信はバグ報告画面から行われるので、その画面にいる状態から送る
                // Real sends happen from the bug-report page, so send while standing on it
                resolver.Resolve<PauseMenuStateService>().ShowPage(PauseMenuPage.BugReport);
                var bundle = await BugReportSubmitUtil.SubmitAndTakeNewBundle(resolver, "テスト報告", PlaytestReportKind.Bug, before);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
                Assert.AreEqual("テスト報告", (string)manifest["description"]);
                BundleManifestContract.AssertPlanC(bundle, manifest);

                var missing = BundleManifestContract.MissingItemNames(manifest);
                Assert.IsFalse(missing.Contains("serverSnapshot"), "サーバースナップショットが欠損扱いになっている");
                Assert.GreaterOrEqual(((JArray)manifest["snapshotFiles"]).Count, 1, "スナップショットが同梱されていない");
                Assert.GreaterOrEqual(((JArray)manifest["snapshotTicks"]).Count, 1, "スナップショットのtickが1つも載っていない");

                // prepare-run.sh は最大tickのスナップショットを save.json に据える。実体が無いと固定ワールド起動ができない
                // prepare-run.sh places the largest-tick snapshot as save.json; without the file a fixed-world boot is impossible
                var latestTick = ((JArray)manifest["snapshotTicks"]).Select(tick => (ulong)tick).Max();
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "snapshots", WorldDataDirectory.SnapshotFileName(latestTick))), $"最大tickのスナップショット実体が無い tick:{latestTick}");
                // manifestのサーバーデータは、この起動が実際にマスタを読んだ置き場でなければならない（違うと再現が解読不能な例外で落ちる）
                // The manifest's server data must be the directory this boot really read masters from; otherwise reproduction dies inscrutably
                Assert.AreEqual(Path.GetFullPath(EditModeInPlayingTestUtil.EditModeInPlayingTestServerDirectoryPath), (string)manifest["serverData"]["path"], "manifestのサーバーデータが起動時の置き場と違う");
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "logs", "unity.log")), "Unityログが同梱されていない");
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "repo", "head.diff")), "未コミット差分が同梱されていない");

                // 録画はテスト起動で止めてあるので、動画は無く欠損として載っていなければならない
                // The recording ring is off in test boots, so the video must be absent and recorded as missing
                Assert.IsFalse(File.Exists(Path.Combine(bundle, "video.mp4")), "録画を止めてあるのに動画がある");
                Assert.IsTrue(missing.Contains("video"), "動画が無いのに欠損にも載っていない");

                await AssertReturnedToPauseMenuTop(resolver);
                Directory.Delete(bundle, true);
            }

            #endregion
        }

        [UnityTest]
        public IEnumerator 確保に失敗しても報告は残り欠けた添付がmanifestに載る()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();

            UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                // リングを開始しないまま報告する。サーバーは即時要求を拒否し、確保は欠損のまま確定する
                // Report without starting the ring: the server rejects the immediate request and the capture settles as missing
                await EditModeInPlayingTestUtil.LoadMainGame();
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                Assert.IsFalse(ServerContext.GetService<WorldSnapshotRing>().IsActive, "常時記録が動いていては拒否経路を通れない");

                var before = BugReportSubmitUtil.ExistingBundles();
                var session = await BugReportSubmitUtil.OpenPauseMenuAndWaitCapture(resolver);
                Assert.IsTrue(session.Status.Value.Missing.Contains("serverSnapshot"), "拒否されたのに確保状態が欠損を持っていない");

                // 実際の送信はバグ報告画面から行われるので、その画面にいる状態から送る
                // Real sends happen from the bug-report page, so send while standing on it
                resolver.Resolve<PauseMenuStateService>().ShowPage(PauseMenuPage.BugReport);
                var bundle = await BugReportSubmitUtil.SubmitAndTakeNewBundle(resolver, "確保に失敗した報告", PlaytestReportKind.Bug, before);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
                Assert.AreEqual("確保に失敗した報告", (string)manifest["description"]);

                // 添付が欠けても残った資料で送る（.decisions 2026-09-11）。運搬対象の印と契約キーは欠損時も揃っている
                // Ship whatever survived even when attachments are missing (.decisions 2026-09-11); the ship marker and contract keys stay
                BundleManifestContract.AssertPlanC(bundle, manifest);
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "logs", "unity.log")), "確保に失敗した報告からUnityログまで落ちている");

                var missing = BundleManifestContract.MissingItemNames(manifest);
                Assert.IsTrue(missing.Contains("serverSnapshot"), "サーバースナップショットの欠損がmanifestに残っていない");
                Assert.IsTrue(missing.Contains("snapshots"), "スナップショット置き場が無かったことがmanifestに残っていない");
                Assert.IsTrue(missing.Contains("video"), "動画の欠損がmanifestに残っていない");
                Assert.IsTrue(missing.Contains("serverData"), "サーバーデータを特定できなかったことがmanifestに残っていない");
                Assert.IsTrue(((JArray)manifest["missing"]).All(item => ((string)item["reason"]).Length > 0), "理由の無い欠損がある");
                Assert.AreEqual(0, ((JArray)manifest["snapshotFiles"]).Count, "確保に失敗したのにスナップショットが載っている");


                await AssertReturnedToPauseMenuTop(resolver);
                Directory.Delete(bundle, true);
            }

            #endregion
        }

        // 送信後はポーズを閉じずにトップへ戻る（ADR 0069）
        // After a send the pause stays open and returns to its top (ADR 0069)
        private static async UniTask AssertReturnedToPauseMenuTop(IObjectResolver resolver)
        {
            var uiState = resolver.Resolve<UIStateControl>();
            var pauseMenu = resolver.Resolve<PauseMenuStateService>();
            for (var i = 0; i < 40 && pauseMenu.CurrentPage.Value != PauseMenuPage.Top; i++) await UniTask.Delay(50);
            Assert.AreEqual(PauseMenuPage.Top, pauseMenu.CurrentPage.Value, "送信後にポーズメニューのトップへ戻っていない");
            Assert.AreEqual(UIStateEnum.PauseMenu, uiState.CurrentState, "送信後にポーズメニューが閉じている");
        }
    }
}
