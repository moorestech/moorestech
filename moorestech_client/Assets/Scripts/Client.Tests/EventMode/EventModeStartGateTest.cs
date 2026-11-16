using Client.Ui.State.Commands;
using Client.Ui.State.EventMode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Client.Localization;
using Client.Starter.EventMode;
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

        private string[] _savedEnvValues;

        [SetUp]
        public void SetUp()
        {
            // イベントモード変数を退避し固定する
            // Save env vars and pin to normal mode.
            _savedEnvValues = EventModeTestEnvironment.Capture(EventModeTestEnvironment.ExhibitionEnableKeys);
            EventModeTestEnvironment.DisableExhibitionMode();

            // 判定基準は公開snapshotの実言語
            // Judged against the real languages of the published snapshot
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // 退避した値へ正確に書き戻す
            // Write the saved values back exactly
            EventModeTestEnvironment.Restore(EventModeTestEnvironment.ExhibitionEnableKeys, _savedEnvValues);
        }

        [Test]
        public void 出展モードでなければ即座に完了し監視も作らない()
        {
            var task = EventModeStartGate.WaitOnGameUiAsync(EventExhibitionSettings.FromEnvironment(), new EventModeStartGateGameUiTest.RecordingGateScreen(new List<string>()), new EventIdleQuitWatcherArmer(), CancellationToken.None);

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
            Assert.IsInstanceOf<UiCommandAccepted>(gate.SelectLanguage("english"));

            yield return order.ToCoroutine();

            Assert.AreEqual(new[] { IdleTimeoutSeconds }, armer.ArmedTimeoutSeconds.ToArray());
            Assert.IsFalse(armer.WasWaitingWhenArmed);
        }

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
