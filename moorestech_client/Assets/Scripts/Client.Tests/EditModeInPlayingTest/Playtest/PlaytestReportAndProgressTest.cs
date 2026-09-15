using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Context;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.Tests.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Playtest
{
    // 種別付きの送信・進行記録の追記・終了での書き出しを1回の起動で通しで固定する（ADR 0058）
    // Pins kind-tagged submission, progress appends and the shutdown write end to end in a single boot (ADR 0058)
    [Category("CiShardClientPlay3")]
    public class PlaytestReportAndProgressTest
    {
        private const string PlacedBlockName = "無限歯車ジェネレーター";
        private static readonly Vector3Int PlacedBlockPosition = new(25, 0, 25);

        [UnityTest]
        public IEnumerator 種別付きで送るとmanifestに載り終了で進行記録が出る()
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

                // 進行記録も常時記録の決定に従うので、リングと同じくこの起動では明示的に開始する
                // The progress record follows the same always-on decision, so this boot starts it explicitly just like the ring
                resolver.Resolve<ProgressRecorder>().StartSession();
                await UniTask.Delay(1000);

                // 進行記録のヘッダはセッション開始時点で書かれている（残骸の回収はこの前に終わっている）
                // The progress header is written at session start, after the leftover recovery has already run
                Assert.IsTrue(ProgressTestSession.HasCurrentSession(), "進行記録のヘッダが書かれていない");

                // 設置の購読が生きているかは、実際に1つ置いて集計に出ることでしか分からない
                // Whether the placement subscription is alive shows only by placing one and seeing it in the aggregate
                EditModeInPlayingTestUtil.PlaceBlock(PlacedBlockName, PlacedBlockPosition, BlockDirection.North);
                await UniTask.Delay(1000);

                var bundlesBefore = BugReportSubmitUtil.ExistingBundles();
                var recordsBefore = ExistingDirectories(GameSystemPaths.ProgressRecordOutboxDirectory);

                // 種別は webui のトグルが載せる契約値。送信経路を通ってmanifestまで届くことがこのテストの主眼
                // The kind is the contract value the webui toggle sends; this test's point is that it reaches the manifest through the submit path
                await BugReportSubmitUtil.OpenPauseMenuAndWaitCapture(resolver);
                var bundle = await BugReportSubmitUtil.SubmitAndTakeNewBundle(resolver, "感想テスト", PlaytestReportKind.Feedback, bundlesBefore);
                AssertManifestCarriesKindAndIdentity(bundle);
                Directory.Delete(bundle, true);

                // 終了パイプラインを回すと、正常終了マーカーと進行記録が揃う
                // Running the shutdown pipeline produces both the clean-exit marker and the progress record
                await Client.Game.Common.GameShutdownEvent.FireGameShutdownAsync(Client.Game.Common.GameShutdownReason.IntentionalExit);
                Assert.IsTrue(File.Exists(CleanExitMarker.CleanMarkerPath(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName)), "正常終了マーカーが書かれていない");
                Assert.IsFalse(ProgressTestSession.HasCurrentSession(), "進行記録が閉じられていない");

                var record = TakeSingleNewDirectory(GameSystemPaths.ProgressRecordOutboxDirectory, recordsBefore, "終了で進行記録が1件だけ増えていない");
                AssertRecordHoldsSubscriptionAndPush(record);
                Directory.Delete(record, true);

                // 後片付け: この起動が置いた開始ゲートの印はPlayMode終了時にPlaytestStartGateBypassが消す
                // Cleanup: the start-gate marks this boot placed are removed by PlaytestStartGateBypass when Play Mode ends
            }

            #endregion
        }

        private static void AssertManifestCarriesKindAndIdentity(string bundle)
        {
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
            Assert.AreEqual("感想テスト", (string)manifest["description"]);
            Assert.AreEqual(PlaytestReportKind.Feedback, (string)manifest["kind"], "選ばれた種別がmanifestに載っていない");

            // 既定の識別は null（開発者のrsync経路）で、取れなかった理由は missing に載る。キー自体が欠けると取り込み側が読む先を失う
            // The default identity is null (the developer rsync path) with the reason in missing; a missing key robs the ingest side of what it reads
            Assert.IsTrue(manifest.ContainsKey("steamId"), "steamId のキーがmanifestに無い");
            Assert.AreEqual(JTokenType.Null, manifest["steamId"].Type, "SteamIDが無いのに実値と同じ形で載っている");
            Assert.IsTrue(((JArray)manifest["missing"]).Any(item => (string)item["item"] == "steamId"), "SteamIDが取れなかった理由がmissingに無い");
            Assert.IsTrue(manifest.ContainsKey("buildInfo"), "buildInfo のキーがmanifestに無い");

            // Editor起動は焼き込み情報を持たないので null。値を名乗ると出所の分からない記録が混ざる
            // An Editor boot has no baked build info, so it is null; claiming a value would mix in records of unknown origin
            Assert.AreEqual(JTokenType.Null, manifest["buildInfo"].Type, "Editor起動なのにbuildInfoが値を名乗っている");
        }

        private static void AssertRecordHoldsSubscriptionAndPush(string record)
        {
            var json = JObject.Parse(File.ReadAllText(Path.Combine(record, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(ProgressEndReason.Quit, (string)json["endReason"], "終了パイプラインを通ったのにquitで閉じられていない");
            Assert.IsTrue(File.Exists(Path.Combine(record, BugReportOutbox.ReadyMarkerFileName)), "READYの無い記録は運搬されない");
            Assert.AreEqual("", (string)json["steamId"], "進行記録のSteamIDが既定の空文字で載っていない");

            var missingItems = ((JArray)json["missing"]).Select(item => (string)item["item"]).ToList();
            CollectionAssert.DoesNotContain(missingItems, "header", "ヘッダのある記録に欠損の印が付いている");

            // UI遷移（購読）と報告送信（プッシュ）の両方が入っていること。片方でも欠けると経路が死んでいる
            // Both the UI transition (subscription) and the report send (push) must be present; one missing means a dead path
            var events = (JArray)json["events"];
            var types = events.Select(entry => (string)entry["type"]).ToList();
            CollectionAssert.Contains(types, ProgressEventType.UiStateChanged, "UI遷移が購読から記録されていない");
            CollectionAssert.Contains(types, ProgressEventType.ReportSent, "報告送信がプッシュから記録されていない");
            CollectionAssert.Contains(types, ProgressEventType.BlockPlaced, "ブロック設置が購読から記録されていない");
            Assert.GreaterOrEqual((int)json["placedBlockCount"], 1, "設置数が集計されていない");

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
