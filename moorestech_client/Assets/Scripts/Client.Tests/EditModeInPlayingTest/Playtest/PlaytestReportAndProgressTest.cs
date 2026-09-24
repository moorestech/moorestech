using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Context;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using Client.Game.InGame.Playtest.Progress.Storage;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.Tests.Playtest;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest.Playtest
{
    // 種別付きの送信・進行記録の購読とプッシュ・終了での書き出しを1回の起動で通しで固定する（ADR 0058）
    // Pins kind-tagged submission, the progress record's subscriptions and pushes, and the shutdown write end to end in a single boot (ADR 0058)
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
                // 進行記録は本番と同じく常時記録の決定だけで始まる。テスト用の開始口は持たない（C15）
                // The progress record starts only from the always-on capture decision, exactly as in production; there is no test-only start (C15)
                AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());
                await EditModeInPlayingTestUtil.LoadMainGame();
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                await UniTask.Delay(1000);

                // 進行記録のヘッダはセッション開始時点で書かれている（残骸の回収はこの前に終わっている）
                // The progress header is written at session start, after the leftover recovery has already run
                Assert.IsTrue(ProgressTestSession.HasCurrentSession(), "進行記録のヘッダが書かれていない");

                // 購読とプッシュが生きているかは、実際に起こして集計に出ることでしか分からない
                // Whether each subscription and push is alive shows only by making it happen and seeing it in the aggregate
                PlaytestProgressDriveUtil.PlaceBlockAsLocalPlayer(PlacedBlockName, PlacedBlockPosition);
                var driven = new DrivenProgress(PlaytestProgressDriveUtil.CraftFirstRecipe(), PlaytestProgressDriveUtil.CompleteResearchWithoutPrerequisites(), PlaytestProgressDriveUtil.CompleteCurrentChallenge());
                await UniTask.Delay(2000);

                var bundlesBefore = BugReportSubmitUtil.ExistingBundles();
                var recordsBefore = ExistingDirectories(GameSystemPaths.ProgressRecordOutboxDirectory);

                // 種別は webui のトグルが載せる契約値。送信経路を通ってmanifestまで届くことを確かめる
                // The kind is the contract value the webui toggle sends; this checks it reaches the manifest through the submit path
                await BugReportSubmitUtil.OpenPauseMenuAndWaitCapture(resolver);
                var (bundle, _) = await BugReportSubmitUtil.SubmitAndTakeNewBundle(resolver, "感想テスト", PlaytestReportKind.Feedback, bundlesBefore);
                AssertManifestCarriesKindAndIdentity(bundle);
                Directory.Delete(bundle, true);

                // 終了パイプラインを回すと、正常終了マーカーと進行記録が揃う
                // Running the shutdown pipeline produces both the clean-exit marker and the progress record
                await Client.Game.Common.GameShutdownEvent.FireGameShutdownAsync(Client.Game.Common.GameShutdownReason.IntentionalExit);
                Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName).ExitedCleanly, "正常終了マーカーが書かれていない");
                Assert.IsFalse(ProgressTestSession.HasCurrentSession(), "進行記録が閉じられていない");

                var record = TakeSingleNewDirectory(GameSystemPaths.ProgressRecordOutboxDirectory, recordsBefore, "終了で進行記録が1件だけ増えていない");
                var json = JObject.Parse(File.ReadAllText(Path.Combine(record, ProgressRecordPaths.RecordFileName)));
                Assert.IsTrue(File.Exists(Path.Combine(record, BugReportOutbox.ReadyMarkerFileName)), "READYの無い記録は運搬されない");
                AssertRecordHoldsSubscriptionAndPush(json);
                AssertRecordHoldsDrivenProgress(json, driven);
                Directory.Delete(record, true);
                AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());

                // 後片付け: この起動が置いた開始ゲートの印はPlayMode終了時にPlaytestStartGateBypassが消す
                // Cleanup: the start-gate marks this boot placed are removed by PlaytestStartGateBypass when Play Mode ends
            }

            #endregion
        }

        private static void AssertManifestCarriesKindAndIdentity(string bundle)
        {
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
            Assert.AreEqual("感想テスト", (string)manifest["description"]);
            Assert.AreEqual("feedback", (string)manifest["kind"], "選ばれた種別がmanifestに載っていない");

            // 既定の識別は null（開発者のrsync経路）で、取れなかった理由は missing に載る。キー自体が欠けると取り込み側が読む先を失う
            // The default identity is null (the developer rsync path) with the reason in missing; a missing key robs the ingest side of what it reads
            Assert.AreEqual(JTokenType.Null, manifest["steamId"].Type, "SteamIDが無いのに実値と同じ形で載っている");
            Assert.IsTrue(((JArray)manifest["missing"]).Any(item => (string)item["item"] == "steamId"), "SteamIDが取れなかった理由がmissingに無い");

            // Editor起動は焼き込み情報を持たないので null。値を名乗ると出所の分からない記録が混ざる
            // An Editor boot has no baked build info, so it is null; claiming a value would mix in records of unknown origin
            Assert.AreEqual(JTokenType.Null, manifest["buildInfo"].Type, "Editor起動なのにbuildInfoが値を名乗っている");
        }

        private static void AssertRecordHoldsSubscriptionAndPush(JObject json)
        {
            Assert.AreEqual("quit", (string)json["endReason"], "終了パイプラインを通ったのにquitで閉じられていない");
            Assert.AreEqual(JTokenType.Null, json["steamId"].Type, "進行記録のSteamIDが取れないのに実値と同じ形で載っている");
            var missingItems = ((JArray)json["missing"]).Select(item => (string)item["item"]).ToList();
            CollectionAssert.Contains(missingItems, "steamId", "SteamIDが取れなかった理由が進行記録のmissingに無い");
            CollectionAssert.DoesNotContain(missingItems, "header", "ヘッダのある記録に欠損の印が付いている");

            // UI遷移（購読）・報告送信（プッシュ）・本人の設置確定（送信口の購読）がそれぞれ入っていること
            // The UI transition (subscription), the report send (push) and this player's placement (send-port subscription) must each be present
            var events = (JArray)json["events"];
            var types = events.Select(entry => (string)entry["type"]).ToList();
            CollectionAssert.Contains(types, UiStateChangedEvent.TypeName, "UI遷移が購読から記録されていない");
            CollectionAssert.Contains(types, ReportSentEvent.TypeName, "報告送信がプッシュから記録されていない");
            CollectionAssert.Contains(types, BlockPlacedEvent.TypeName, "本人のブロック設置が記録されていない");
            Assert.GreaterOrEqual((int)json["placedBlockCount"], 1, "設置数が集計されていない");

            var reportSent = events.First(entry => (string)entry["type"] == ReportSentEvent.TypeName);
            Assert.AreEqual("feedback", (string)reportSent["data"]["kind"], "送った種別が進行記録に残っていない");
            Assert.AreEqual(UIStateEnum.PauseMenu.ToString(), (string)json["lastUiState"], "最後のUI状態が集計されていない");
            Assert.Greater((double)json["playSeconds"], 0, "プレイ時間が0秒のまま記録されている");
        }

        // クラフト・研究・チャレンジの購読を戻すと、ここの種別・件数・GUIDのどれかが必ず赤くなる（C23）
        // Reverting any of the craft, research or challenge subscriptions always turns one of these type, count or GUID checks red (C23)
        private static void AssertRecordHoldsDrivenProgress(JObject json, DrivenProgress driven)
        {
            var events = (JArray)json["events"];
            var craftEvents = events.Where(entry => (string)entry["type"] == CraftCompletedEvent.TypeName).ToList();
            Assert.IsTrue(craftEvents.Any(entry => (string)entry["data"]["recipeGuid"] == driven.CraftRecipeGuid.ToString()), "成立したクラフトのレシピが進行記録に無い");
            Assert.GreaterOrEqual((int)json["craftCount"], 1, "クラフト数が集計されていない");

            Assert.IsTrue(events.Any(entry => (string)entry["type"] == ResearchCompletedEvent.TypeName && (string)entry["data"]["researchGuid"] == driven.ResearchGuid.ToString()), "完了した研究のイベントが進行記録に無い");
            CollectionAssert.Contains(((JArray)json["completedResearch"]).Select(guid => (string)guid).ToList(), driven.ResearchGuid.ToString(), "完了した研究が集計されていない");

            Assert.IsTrue(events.Any(entry => (string)entry["type"] == ChallengeCompletedEvent.TypeName && (string)entry["data"]["challengeGuid"] == driven.ChallengeGuid.ToString()), "完了したチャレンジのイベントが進行記録に無い");
            CollectionAssert.Contains(((JArray)json["reachedChallenges"]).Select(guid => (string)guid).ToList(), driven.ChallengeGuid.ToString(), "完了したチャレンジが集計されていない");
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

        private readonly struct DrivenProgress
        {
            public readonly Guid CraftRecipeGuid;
            public readonly Guid ResearchGuid;
            public readonly Guid ChallengeGuid;

            public DrivenProgress(Guid craftRecipeGuid, Guid researchGuid, Guid challengeGuid)
            {
                CraftRecipeGuid = craftRecipeGuid;
                ResearchGuid = researchGuid;
                ChallengeGuid = challengeGuid;
            }
        }
    }
}
