using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.Playtest.Progress;
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

namespace Client.Tests.EditModeInPlayingTest.Playtest
{
    // 種別付きの送信・進行記録の追記・終了での書き出しを1回の起動で通しで固定する（ADR 0058）
    // Pins kind-tagged submission, progress appends and the shutdown write end to end in a single boot (ADR 0058)
    [Category("CiShardClientPlay3")]
    public class PlaytestReportAndProgressTest
    {
        [UnityTest]
        public IEnumerator 種別付きで送るとmanifestに載り終了で進行記録が出る()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(true);
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

                // 進行記録のヘッダはセッション開始時点で書かれている（残骸の回収はこの前に終わっている）
                // The progress header is written at session start, after the leftover recovery has already run
                Assert.IsTrue(ProgressRecordFiles.HasCurrentSession(), "進行記録のヘッダが書かれていない");

                var bundlesBefore = ExistingDirectories(GameSystemPaths.BugReportOutboxDirectory);
                var recordsBefore = ExistingDirectories(GameSystemPaths.ProgressRecordOutboxDirectory);

                var bundle = await SubmitFeedbackAndTakeNewBundle(resolver, bundlesBefore);
                AssertManifestCarriesKindAndIdentity(bundle);
                Directory.Delete(bundle, true);

                // 終了パイプラインを回すと、正常終了マーカーと進行記録が揃う
                // Running the shutdown pipeline produces both the clean-exit marker and the progress record
                await Client.Game.Common.GameShutdownEvent.FireGameShutdownAsync();
                Assert.IsTrue(File.Exists(CleanExitMarker.FilePath), "正常終了マーカーが書かれていない");
                Assert.IsFalse(ProgressRecordFiles.HasCurrentSession(), "進行記録が閉じられていない");

                var record = TakeSingleNewDirectory(GameSystemPaths.ProgressRecordOutboxDirectory, recordsBefore, "終了で進行記録が1件だけ増えていない");
                AssertRecordHoldsSubscriptionAndPush(record);
                Directory.Delete(record, true);

                // 後片付け: 次のテスト起動が「前回異常終了」にならないよう正常終了マーカーは残す
                // Cleanup: the clean-exit marker stays so the next test boot is not treated as an unclean exit
            }

            #endregion
        }

        // 種別は webui のトグルが載せる契約値。送信経路を通ってmanifestまで届くことがこのテストの主眼
        // The kind is the contract value the webui toggle sends; this test's point is that it reaches the manifest through the submit path
        private static async UniTask<string> SubmitFeedbackAndTakeNewBundle(IObjectResolver resolver, IReadOnlyCollection<string> before)
        {
            var session = resolver.Resolve<BugReportCaptureSession>();
            resolver.Resolve<UIStateControl>().RequestTransition(UIStateEnum.PauseMenu);

            // サーバー確保の打ち切り上限は15秒なので、それより長く待って確定を見届ける
            // The server capture gives up after 15 seconds, so wait longer than that to see it settle
            for (var i = 0; i < 400 && session.Status.Value.Kind != BugReportCaptureStatus.Ready; i++) await UniTask.Delay(50);
            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind, "ポーズメニューを開いても20秒以内に送信できる状態にならない");

            var handler = new BugReportSubmitActionHandler(
                resolver.Resolve<BugReportBundleWriter>(),
                session,
                resolver.Resolve<UIStateControl>(),
                resolver.Resolve<IPlaytestProgressSink>());
            var result = await handler.ExecuteAsync(new JObject { ["description"] = "感想テスト", ["kind"] = PlaytestReportKind.Feedback });
            Assert.IsTrue(result.Ok, result.Error);

            return TakeSingleNewDirectory(GameSystemPaths.BugReportOutboxDirectory, before, "送信でoutboxに増えた箱が1つではない");
        }

        private static void AssertManifestCarriesKindAndIdentity(string bundle)
        {
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
            Assert.AreEqual("感想テスト", (string)manifest["description"]);
            Assert.AreEqual(PlaytestReportKind.Feedback, (string)manifest["kind"], "選ばれた種別がmanifestに載っていない");

            // 既定の識別は空文字（開発者のrsync経路）。キー自体が欠けると取り込み側が読む先を失う
            // The default identity is an empty string (the developer rsync path); a missing key robs the ingest side of what it reads
            Assert.AreEqual("", (string)manifest["steamId"], "SteamIDが既定の空文字で載っていない");
            Assert.IsTrue(manifest.ContainsKey("buildInfo"), "buildInfo のキーがmanifestに無い");

            // Editor起動は焼き込み情報を持たないので null。値を名乗ると出所の分からない記録が混ざる
            // An Editor boot has no baked build info, so it is null; claiming a value would mix in records of unknown origin
            Assert.AreEqual(JTokenType.Null, manifest["buildInfo"].Type, "Editor起動なのにbuildInfoが値を名乗っている");
        }

        private static void AssertRecordHoldsSubscriptionAndPush(string record)
        {
            var json = JObject.Parse(File.ReadAllText(Path.Combine(record, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(ProgressSessionRecovery.QuitEndReason, (string)json["endReason"], "終了パイプラインを通ったのにquitで閉じられていない");
            Assert.IsTrue(File.Exists(Path.Combine(record, ProgressRecordPaths.ReadyMarkerFileName)), "READYの無い記録は運搬されない");
            Assert.AreEqual("", (string)json["steamId"], "進行記録のSteamIDが既定の空文字で載っていない");
            Assert.IsFalse(json.ContainsKey("headerMissing"), "ヘッダのある記録に欠損の印が付いている");

            // UI遷移（購読）と報告送信（プッシュ）の両方が入っていること。片方でも欠けると経路が死んでいる
            // Both the UI transition (subscription) and the report send (push) must be present; one missing means a dead path
            var events = (JArray)json["events"];
            var types = events.Select(entry => (string)entry["type"]).ToList();
            CollectionAssert.Contains(types, ProgressEventType.UiStateChanged, "UI遷移が購読から記録されていない");
            CollectionAssert.Contains(types, ProgressEventType.ReportSent, "報告送信がプッシュから記録されていない");

            var reportSent = events.First(entry => (string)entry["type"] == ProgressEventType.ReportSent);
            Assert.AreEqual(PlaytestReportKind.Feedback, (string)reportSent["data"]["kind"], "送った種別が進行記録に残っていない");
            Assert.AreEqual(UIStateEnum.PauseMenu.ToString(), (string)json["lastUiState"], "最後のUI状態が集計されていない");
            Assert.Greater((double)json["playSeconds"], 0, "プレイ時間が0秒のまま記録されている");
        }

        private static string TakeSingleNewDirectory(string root, IReadOnlyCollection<string> before, string message)
        {
            var added = ExistingDirectories(root).Except(before).ToList();
            Assert.AreEqual(1, added.Count, message);
            return added[0];
        }

        private static List<string> ExistingDirectories(string root)
        {
            Directory.CreateDirectory(root);
            return Directory.GetDirectories(root).ToList();
        }
    }
}
