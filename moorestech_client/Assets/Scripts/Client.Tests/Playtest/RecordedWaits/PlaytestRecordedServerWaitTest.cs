using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Playtest;
using Client.Playtest.Core;
using Client.Playtest.Operations;
using Client.Playtest.Overlay;
using Client.Tests.Starter;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Research;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.RecordedWaits
{
    public class PlaytestRecordedServerWaitTest
    {
        private string _worldRoot;
        private ServerStarter _starter;
        private PlaytestResult _result;
        private PlaytestDriver _driver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 実サーバーのtick開始を待つ。UnitySetUpはEditModeでyield反復子の状態欄を読むためToCoroutineを使わない
            // Wait for the real server's tick boundary; EditMode UnitySetUp reads the yield iterator state field, so avoid ToCoroutine
            _worldRoot = ServerStarterTestBoot.NewWorldRoot();
            _starter = ServerStarterTestBoot.Boot(_worldRoot);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!(ServerThreadActionQueueAccess.Current?.HasDrainedThisLifetime ?? false) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ServerThreadActionQueueAccess.Current?.HasDrainedThisLifetime ?? false);
            PlaytestOverlay.State.Reset();
            _result = new PlaytestResult();
            _driver = new PlaytestDriver(_result, _worldRoot);
            yield return _driver.RunOnServer("研究を未完了へ戻す", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject())).ToCoroutine();
        }

        [TearDown]
        public void TearDown()
        {
            ServerStarterTestBoot.Teardown(_starter);
            if (Directory.Exists(_worldRoot)) Directory.Delete(_worldRoot, true);
            PlaytestOverlay.State.Reset();
        }

        [UnityTest]
        public IEnumerator SuccessfulResearchWaitRecordsItsEvaluatedResult() => UniTask.ToCoroutine(async () =>
        {
            const string label = "UI からアップグレード Lv2 を研究した";
            var research = ForUnitTestLevelInheritanceIds.CraftCapResearchGuid;
            var wait = _driver.WaitForResearch(new[] { research }, Array.Empty<Guid>(), 10f, label);
            await UniTask.DelayFrame(3);
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);
            Assert.IsEmpty(_result.Asserts, "条件が偽の間に成功が記録されている");

            // 実研究の変化でWaitを成功させる
            // The wait succeeds as a result of changing the actual research state
            await _driver.RunOnServer("保存済み研究の完了状態を復元する", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(
                new ResearchSaveJsonObject { CompletedResearchGuids = new List<string> { research.ToString() } }));
            await wait;
            AssertRecordedResult(label, true);
            Assert.That(PlaytestOverlay.State.LogEntries.Single(entry => entry.Kind == PlaytestOverlayLogKind.Wait).Text, Does.EndWith(" ✔"));
        });

        [UnityTest]
        public IEnumerator ResearchTimeoutRecordsFailureAndThrows()
        {
            const string label = "未完了の研究を待つ";
            var wait = _driver.WaitForResearch(new[] { ForUnitTestLevelInheritanceIds.CraftCapResearchGuid }, Array.Empty<Guid>(), 0f, label);
            yield return WaitForCompletion(wait);
            Assert.Throws<TimeoutException>(() => wait.GetAwaiter().GetResult());
            AssertRecordedResult(label, false);
        }

        [UnityTest]
        public IEnumerator ChallengeTimeoutUsesTheSameFailureContract()
        {
            const string label = "存在しない完了チャレンジを待つ";
            var wait = _driver.WaitForChallenge(Guid.NewGuid(), true, 0f, label);
            yield return WaitForCompletion(wait);
            Assert.Throws<TimeoutException>(() => wait.GetAwaiter().GetResult());
            AssertRecordedResult(label, false);
        }

        private void AssertRecordedResult(string label, bool passed)
        {
            // 保存JSONにWAITと成否を残す
            // A result.json-equivalent JSON round trip retains one WAIT and one outcome
            var saved = JsonUtility.FromJson<PlaytestResult>(JsonUtility.ToJson(_result));
            Assert.AreEqual(1, saved.Asserts.Count);
            Assert.AreEqual(label, saved.Asserts.Single().Label);
            Assert.AreEqual(passed, saved.Asserts.Single().Passed);
            Assert.AreEqual(1, saved.Timeline.Count(line => line.Contains($"[WAIT] {label}")));
            Assert.AreEqual(1, saved.Timeline.Count(line => line.Contains($"[{(passed ? "PASS" : "FAIL")}] {label}")));
        }

        private static IEnumerator WaitForCompletion(UniTask wait)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (wait.Status == UniTaskStatus.Pending && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(UniTaskStatus.Pending, wait.Status, "待機の期限が効いていない");
        }
    }
}
