using System;
using System.Collections;
using System.Collections.Generic;
using Client.Localization;
using Client.Starter.EventMode;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.EventMode
{
    public class EventModeStartGateTest
    {
        private const int IdleTimeoutSeconds = 180;

        [SetUp]
        public void SetUp()
        {
            // TrySetLanguageは公開snapshotの実言語を判定基準にするため、辞書を張ってから検証する
            // TrySetLanguage judges against the published snapshot, so the dictionaries must be loaded first
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE", null);
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE_EDITOR", null);
        }

        [Test]
        public void 出展モードでなければ即座に完了し監視も作らない()
        {
            var task = EventModeStartGate.WaitForLanguageSelectionAsync();

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            Assert.AreEqual(0, Object.FindObjectsByType<EventIdleQuitWatcher>(FindObjectsSortMode.None).Length);
        }

        // PRの中核である「言語選択を待ってから武装する」順序を、武装窓口の呼ばれ方で押さえる
        // Pins the PR's core order — wait for the selection, then arm — through how the arming window is called
        [Test]
        public void 言語が選ばれるまで無操作監視を武装しない()
        {
            var gate = new EventLanguageGate(true);
            var armer = new RecordingIdleWatchArmer(gate);

            var order = EventModeStartGate.AwaitSelectionThenArmAsync(gate, IdleTimeoutSeconds, armer);

            Assert.IsFalse(order.Status.IsCompleted());
            Assert.AreEqual(0, armer.ArmedTimeoutSeconds.Count);
        }

        [UnityTest]
        public IEnumerator 言語選択の後に待機が解けた状態で一度だけ武装する()
        {
            var gate = new EventLanguageGate(true);
            var armer = new RecordingIdleWatchArmer(gate);

            var order = EventModeStartGate.AwaitSelectionThenArmAsync(gate, IdleTimeoutSeconds, armer);
            Assert.AreEqual(EventLanguageSelectionResult.Applied, gate.TrySelectLanguage("english"));

            yield return order.ToCoroutine();

            Assert.AreEqual(new[] { IdleTimeoutSeconds }, armer.ArmedTimeoutSeconds.ToArray());
            Assert.IsFalse(armer.WasWaitingWhenArmed);
        }

        // WebUiHost未起動のフォールバックはEditModeで検証できない（DontDestroyOnLoadがPlayMode専用APIのため）。
        // The no-WebUiHost fallback is not EditMode-testable because DontDestroyOnLoad is a PlayMode-only API.
        // 当該分岐はコードレビューとReleaseビルドの通し確認（ADR 0040「実機確認」）で担保する。
        // That branch is covered by code review and the Release build walkthrough recorded in ADR 0040.

        // 武装の呼ばれた回数・引数と、その瞬間のゲート状態を記録して順序を観測する
        // Records the arming calls, their argument, and the gate state at that moment to observe the order
        private class RecordingIdleWatchArmer : IEventIdleWatchArmer
        {
            private readonly EventLanguageGate _gate;

            public readonly List<int> ArmedTimeoutSeconds = new();
            public bool WasWaitingWhenArmed { get; private set; }

            public RecordingIdleWatchArmer(EventLanguageGate gate)
            {
                _gate = gate;
                WasWaitingWhenArmed = true;
            }

            public void ArmIdleWatch(int idleTimeoutSeconds)
            {
                ArmedTimeoutSeconds.Add(idleTimeoutSeconds);
                WasWaitingWhenArmed = _gate.IsWaitingSelection;
            }
        }
    }
}
