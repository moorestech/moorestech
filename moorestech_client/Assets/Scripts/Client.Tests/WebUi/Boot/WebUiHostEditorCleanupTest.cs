#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void 初期化失敗後のPlay停止は正常終了へ書き換えない()
        {
            var reasons = new List<GameShutdownReason>();
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reasons.Add);

            // 初期化失敗の後に届くEditor停止でも、最初の終了理由を維持する
            // Preserve the original shutdown reason when an Editor stop follows initialization failure
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
