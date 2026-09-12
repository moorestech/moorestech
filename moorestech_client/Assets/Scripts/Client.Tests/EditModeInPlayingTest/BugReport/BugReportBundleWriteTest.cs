using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Actions;
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
                ServerContext.GetService<WorldSnapshotRing>().Start(SnapshotRingConfig.PeriodTicks, SnapshotRingConfig.RetentionTicks, SnapshotRingConfig.MaxGenerations);
                await UniTask.Delay(1000);

                var before = ExistingBundles();
                var session = await OpenPauseMenuAndWaitCapture(resolver);
                Assert.IsFalse(session.Status.Value.Missing.Contains("serverSnapshot"), "サーバースナップショットの確保に失敗した");

                var bundle = await SubmitAndTakeNewBundle(resolver, "テスト報告", before);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
                Assert.AreEqual("テスト報告", (string)manifest["description"]);
                AssertPlanCManifestContract(bundle, manifest);

                var missing = MissingItems(manifest);
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

                await AssertReturnedToGameScreen(resolver);
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

                var before = ExistingBundles();
                var session = await OpenPauseMenuAndWaitCapture(resolver);
                Assert.IsTrue(session.Status.Value.Missing.Contains("serverSnapshot"), "拒否されたのに確保状態が欠損を持っていない");

                var bundle = await SubmitAndTakeNewBundle(resolver, "確保に失敗した報告", before);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
                Assert.AreEqual("確保に失敗した報告", (string)manifest["description"]);

                // 添付が欠けても残った資料で送る（.decisions 2026-09-11）。運搬対象の印と契約キーは欠損時も揃っている
                // Ship whatever survived even when attachments are missing (.decisions 2026-09-11); the ship marker and contract keys stay
                AssertPlanCManifestContract(bundle, manifest);
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "logs", "unity.log")), "確保に失敗した報告からUnityログまで落ちている");

                var missing = MissingItems(manifest);
                Assert.IsTrue(missing.Contains("serverSnapshot"), "サーバースナップショットの欠損がmanifestに残っていない");
                Assert.IsTrue(missing.Contains("snapshots"), "スナップショット置き場が無かったことがmanifestに残っていない");
                Assert.IsTrue(missing.Contains("video"), "動画の欠損がmanifestに残っていない");
                Assert.IsTrue(missing.Contains("serverData"), "サーバーデータを特定できなかったことがmanifestに残っていない");
                Assert.IsTrue(((JArray)manifest["missing"]).All(item => ((string)item["reason"]).Length > 0), "理由の無い欠損がある");
                Assert.AreEqual(0, ((JArray)manifest["snapshotFiles"]).Count, "確保に失敗したのにスナップショットが載っている");

                await AssertReturnedToGameScreen(resolver);
                Directory.Delete(bundle, true);
            }

            #endregion
        }

        private static async UniTask<BugReportCaptureSession> OpenPauseMenuAndWaitCapture(IObjectResolver resolver)
        {
            var session = resolver.Resolve<BugReportCaptureSession>();
            resolver.Resolve<UIStateControl>().RequestTransition(UIStateEnum.PauseMenu);

            // サーバー確保の打ち切り上限は15秒なので、それより長く待って確定を見届ける
            // The server capture gives up after 15 seconds, so wait longer than that to see it settle
            for (var i = 0; i < 400 && (!session.Status.Value.HasSession || session.Status.Value.CapturePending); i++) await UniTask.Delay(50);

            Assert.IsTrue(session.Status.Value.HasSession, "ポーズメニューを開いても確保セッションが始まっていない");
            Assert.IsFalse(session.Status.Value.CapturePending, "サーバー確保の待ちが20秒以内に確定しない");
            return session;
        }

        private static async UniTask<string> SubmitAndTakeNewBundle(IObjectResolver resolver, string description, IReadOnlyCollection<string> before)
        {
            var handler = new BugReportSubmitActionHandler(resolver.Resolve<BugReportBundleWriter>(), resolver.Resolve<BugReportCaptureSession>(), resolver.Resolve<UIStateControl>());
            var result = await handler.ExecuteAsync(new JObject { ["description"] = description });
            Assert.IsTrue(result.Ok, result.Error);

            var added = ExistingBundles().Except(before).ToList();
            Assert.AreEqual(1, added.Count, "送信でoutboxに増えた箱が1つではない");
            return added[0];
        }

        // plan C の prepare-run.sh / ship-outbox.sh が読むキー。欠けると Mac mini 側が最初の1行で落ちる
        // The keys plan C's prepare-run.sh and ship-outbox.sh read; missing one kills the Mac mini side on its first line
        private static void AssertPlanCManifestContract(string bundle, JObject manifest)
        {
            Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportOutbox.ReadyMarkerFileName)), "READYが無い箱は運搬されない");
            Assert.IsNotNull(manifest["repository"], "prepare-run.sh が読む repository が無い");
            // 40桁で確認する。スレッド違反でgitを呼べていないと空文字のまま通ってしまう
            // Checked as 40 digits: a thread violation that never reaches git would slip through as an empty string
            Assert.AreEqual(40, ((string)manifest["repository"]["commit"]).Length, "prepare-run.sh が読む repository.commit がコミットハッシュでない");
            Assert.IsNotNull((string)manifest["repository"]["branch"], "prepare-run.sh が読む repository.branch が無い");
            Assert.IsNotNull(manifest["masterData"], "prepare-run.sh が読む masterData が無い");
            Assert.IsNotNull((string)manifest["masterData"]["commit"], "prepare-run.sh が読む masterData.commit が無い");
            Assert.IsInstanceOf<JArray>(manifest["snapshotTicks"], "prepare-run.sh が読む snapshotTicks が配列でない");
            Assert.IsInstanceOf<JArray>(manifest["missing"], "missing が配列でない");
        }

        // 欠損は同じ項目名が複数回載りうる（確保側と書き出し側の両方が理由を足す）ため一覧のまま扱う
        // The same item can appear more than once (both capture and writer add reasons), so keep it as a list
        private static List<string> MissingItems(JObject manifest)
        {
            return ((JArray)manifest["missing"]).Select(item => (string)item["item"]).ToList();
        }

        private static async UniTask AssertReturnedToGameScreen(IObjectResolver resolver)
        {
            var uiState = resolver.Resolve<UIStateControl>();
            for (var i = 0; i < 40 && uiState.CurrentState != UIStateEnum.GameScreen; i++) await UniTask.Delay(50);
            Assert.AreEqual(UIStateEnum.GameScreen, uiState.CurrentState, "送信後にポーズメニューが閉じていない");
        }

        private static List<string> ExistingBundles()
        {
            Directory.CreateDirectory(GameSystemPaths.BugReportOutboxDirectory);
            return Directory.GetDirectories(GameSystemPaths.BugReportOutboxDirectory).ToList();
        }
    }
}
