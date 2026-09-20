using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Client.Localization;
using Client.Starter.EventMode;
using Client.Tests.WebUi.Gate;
using Client.WebUiHost.Boot;
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

        private static readonly string[] SavedEnvKeys = { EventExhibitionSettings.EnableEnvKey, EventExhibitionSettings.EditorOptInEnvKey };
        private readonly string[] _savedEnvValues = new string[SavedEnvKeys.Length];

        [SetUp]
        public void SetUp()
        {
            // イベントモード変数を退避し固定する
            // Save env vars and pin to normal mode.
            for (var i = 0; i < SavedEnvKeys.Length; i++) _savedEnvValues[i] = Environment.GetEnvironmentVariable(SavedEnvKeys[i]);
            for (var i = 0; i < SavedEnvKeys.Length; i++) Environment.SetEnvironmentVariable(SavedEnvKeys[i], null);

            // TrySetLanguageは公開snapshotの実言語を判定基準にするため、辞書を張ってから検証する
            // TrySetLanguage judges against the published snapshot, so the dictionaries must be loaded first
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // 退避した値へ正確に書き戻す
            // Write the saved values back exactly
            for (var i = 0; i < SavedEnvKeys.Length; i++) Environment.SetEnvironmentVariable(SavedEnvKeys[i], _savedEnvValues[i]);
        }

        [Test]
        public void 出展モードでなければ即座に完了し監視も作らない()
        {
            var task = EventModeStartGate.WaitForLanguageSelectionAsync(CancellationToken.None);

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            Assert.AreEqual(0, Object.FindObjectsByType<EventIdleQuitWatcher>(FindObjectsSortMode.None).Length);
        }

        // 通常モードでも言語ゲートのtopicは登録する。Web側は無条件に購読し、未登録だとWS再接続後にrestoringが解けず全UIが操作不能になる
        // The language-gate topic is registered even outside exhibition mode; the web subscribes unconditionally, and a missing topic leaves restoring stuck after a WS reconnect
        [Test]
        public void 出展モードでなくても言語ゲートのtopicとactionを待機なしで登録する()
        {
            var hub = new WebSocketHub();

            var task = EventModeStartGate.WaitForLanguageSelectionWithHubAsync(hub, EventExhibitionSettings.FromEnvironment(), CancellationToken.None);

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            StartGateTopicAssert.AssertWaiting(hub, EventLanguageGateTopic.TopicName, false);
            Assert.IsNotNull(hub.ResolveAction("event_mode.select_language"));
        }

        // PRの中核である「言語選択を待ってから武装する」順序を、武装窓口の呼ばれ方で押さえる
        // Pins the PR's core order — wait for the selection, then arm — through how the arming window is called
        [Test]
        public void 言語が選ばれるまで無操作監視を武装しない()
        {
            var gate = new EventLanguageGate(true);
            var armer = new RecordingIdleWatchArmer(gate);

            var order = EventModeStartGate.AwaitSelectionThenArmAsync(gate, IdleTimeoutSeconds, armer, CancellationToken.None);

            Assert.IsFalse(order.Status.IsCompleted());
            Assert.AreEqual(0, armer.ArmedTimeoutSeconds.Count);
        }

        // 終了のキャンセルが来たら選択を待たずに抜け、武装もしない
        // An exit cancellation leaves without waiting for the choice and never arms
        [Test]
        public void 終了のキャンセルで言語選択の待ちを打ち切り武装しない()
        {
            var gate = new EventLanguageGate(true);
            var armer = new RecordingIdleWatchArmer(gate);
            using var exit = new CancellationTokenSource();

            var order = EventModeStartGate.AwaitSelectionThenArmAsync(gate, IdleTimeoutSeconds, armer, exit.Token);
            exit.Cancel();

            Assert.IsTrue(order.Status.IsCanceled());
            Assert.AreEqual(0, armer.ArmedTimeoutSeconds.Count);
        }

        [UnityTest]
        public IEnumerator 言語選択の後に待機が解けた状態で一度だけ武装する()
        {
            var gate = new EventLanguageGate(true);
            var armer = new RecordingIdleWatchArmer(gate);

            var order = EventModeStartGate.AwaitSelectionThenArmAsync(gate, IdleTimeoutSeconds, armer, CancellationToken.None);
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
