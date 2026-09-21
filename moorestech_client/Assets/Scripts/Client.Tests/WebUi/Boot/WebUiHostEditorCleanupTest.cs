#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Host = Client.WebUiHost.Boot.WebUiHost;

namespace Client.Tests.WebUi.Boot
{
    public class WebUiHostEditorCleanupTest
    {
        private const int TestProcessId = 999021;
        private const string UnawaitableExitWarning = "書き出し完了を待てない終了のため、意思表明の時点で正常終了として記録します（終了処理中の停止はこの経路では検知できません）";
        private string _sessionName;
        private ControllableShutdownParticipant _participant;

        [SetUp]
        public void SetUp()
        {
            // 実セッションや他のテストの印を消さない専用識別子を使う
            // Use a dedicated identity so cleanup never removes real sessions or other tests' marks
            _sessionName = $"session_{DateTime.UtcNow.Ticks}";
            CleanExitMarkWriter.ClearSubscriptions();
            GameShutdownEvent.ResetForNewSession();
            CleanExitMarker.ConsumeSessionMarks(TestProcessId, _sessionName);
            CleanExitMarkWriter.InstallAtStartup(TestProcessId, _sessionName);
            _participant = new ControllableShutdownParticipant();
        }

        [TearDown]
        public void TearDown()
        {
            // 保留した書き出しを閉じてから印を消し、後続テストへの遅延書き込みを防ぐ
            // Finish the pending flush before removing marks to prevent writes leaking into later tests
            _participant.Complete();
            CleanExitMarkWriter.ClearSubscriptions();
            CleanExitMarker.ConsumeSessionMarks(TestProcessId, _sessionName);
            GameShutdownEvent.ResetForNewSession();
        }

        [Test]
        public void Play停止は書き出し完了前に正常終了の印を書く()
        {
            GameShutdownEvent.RegisterParticipant(_participant);
            LogAssert.Expect(LogType.Warning, UnawaitableExitWarning);

            WebUiHostEditorCleanup.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);

            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, _sessionName);
            Assert.IsTrue(record.ExitedCleanly, "Editor停止がshutdownパイプラインへ届かず、正常終了の印が無い");
            Assert.IsFalse(record.ShutdownStalled);
        }

        [TestCase(PlayModeStateChange.EnteredEditMode)]
        [TestCase(PlayModeStateChange.ExitingEditMode)]
        [TestCase(PlayModeStateChange.EnteredPlayMode)]
        public void Play停止以外では終了を通知しない(PlayModeStateChange state)
        {
            var reasons = new List<GameShutdownReason>();
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reasons.Add);

            WebUiHostEditorCleanup.OnPlayModeStateChanged(state);

            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, _sessionName);
            Assert.IsEmpty(reasons);
            Assert.IsFalse(record.ExitedCleanly);
            Assert.IsFalse(record.ShutdownStalled);
        }

        [Test]
        public void 重複したPlay停止は待てない終了を一度だけ通知する()
        {
            var reasons = new List<GameShutdownReason>();
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reasons.Add);
            LogAssert.Expect(LogType.Warning, UnawaitableExitWarning);

            WebUiHostEditorCleanup.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            WebUiHostEditorCleanup.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);

            CollectionAssert.AreEqual(new[] { GameShutdownReason.UnawaitableExit }, reasons);
            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(TestProcessId, _sessionName).ExitedCleanly);
        }

        [Test]
        public void Play停止は通知で開始した同じホスト停止の完了を同期で待つ()
        {
            var hubField = typeof(Host).GetField("_hub", BindingFlags.Static | BindingFlags.NonPublic);
            var stopTaskField = typeof(Host).GetField("_stopTask", BindingFlags.Static | BindingFlags.NonPublic);
            var hub = new WebSocketHub();
            var topic = new BlockingShutdownTopic();
            hub.RegisterTopic("shutdown-test", topic);
            hubField.SetValue(null, hub);

            using var notified = new ManualResetEventSlim();
            using var callbackReturned = new ManualResetEventSlim();
            Task firstStopTask = null;
            var cleanupFinishedBeforeNotification = false;
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(_ =>
            {
                cleanupFinishedBeforeNotification = topic.DisposalCompleted;
                Host.Stop();
                firstStopTask = (Task)stopTaskField.GetValue(null);
                notified.Set();
            });

            // 実StopAsyncのバインド解除を止め、callbackの早期復帰を別スレッドから検知する
            // Block binding cleanup inside the real StopAsync and detect early callback return on another thread
            var observation = Task.Run(() =>
            {
                var notificationArrived = notified.Wait(TimeSpan.FromSeconds(2));
                var disposalStarted = topic.WaitForDisposal();
                var returnedBeforeRelease = callbackReturned.Wait(TimeSpan.FromMilliseconds(100));
                topic.Release();
                return (notificationArrived, disposalStarted, returnedBeforeRelease);
            });
            LogAssert.Expect(LogType.Warning, UnawaitableExitWarning);
            WebUiHostEditorCleanup.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            var stopCompletedOnReturn = firstStopTask?.IsCompleted == true;
            callbackReturned.Set();
            var observed = observation.GetAwaiter().GetResult();
            firstStopTask?.GetAwaiter().GetResult();

            Assert.IsTrue(observed.notificationArrived);
            Assert.IsTrue(observed.disposalStarted);
            Assert.IsFalse(cleanupFinishedBeforeNotification, "shutdown通知より前に同期cleanupが完了した");
            Assert.AreSame(firstStopTask, stopTaskField.GetValue(null), "未完了の停止Taskが別の停止Taskで上書きされた");
            Assert.IsFalse(observed.returnedBeforeRelease, "実ホストの停止を待たずcallbackが復帰した");
            Assert.IsTrue(stopCompletedOnReturn, "callback復帰時に最初の停止Taskが未完了だった");
        }

        [Test]
        public void 初期化失敗後のPlay停止は正常終了へ書き換えない()
        {
            var reasons = new List<GameShutdownReason>();
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reasons.Add);

            GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);
            WebUiHostEditorCleanup.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);

            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, _sessionName);
            CollectionAssert.AreEqual(new[] { GameShutdownReason.InitializationFailed }, reasons);
            Assert.IsFalse(record.ExitedCleanly);
            Assert.IsFalse(record.ShutdownStalled);
        }

        // PlayerLoop無しでも未完了の書き出しを保持する
        // Keep the flush pending without requiring a PlayerLoop
        private sealed class ControllableShutdownParticipant : IGameShutdownParticipant
        {
            private readonly UniTaskCompletionSource<ShutdownFlushResult> _completionSource = new();

            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                return _completionSource.Task;
            }

            public void Complete()
            {
                _completionSource.TrySetResult(ShutdownFlushResult.Flushed);
            }
        }
    }
}
#endif
