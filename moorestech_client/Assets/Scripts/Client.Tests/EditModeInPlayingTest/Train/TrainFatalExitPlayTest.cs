using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Starter;
using Client.Tests.TickSynchronization;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay3")]
    public class TrainFatalExitPlayTest
    {
        private const int ProcessId = 999003;
        private const string Session = "fatal_playmode_test";

        [UnityTest]
        public IEnumerator InitialSnapshotFailure_StartupBoundaryQuitsPlayWithoutSaving()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            yield return new FailedSnapshotExit();
            Assert.IsFalse(EditorApplication.isPlaying);
            Assert.AreEqual(1, SessionState.GetInt("TrainFatalExit_Notices", 0));
            Assert.AreEqual(0, SessionState.GetInt("TrainFatalExit_Flushes", 0));
            var marks = CleanExitMarker.ConsumeSessionMarks(ProcessId, Session);
            Assert.IsFalse(marks.ExitedCleanly);
            Assert.IsFalse(marks.ShutdownStalled);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            GameShutdownEvent.ResetForNewSession();
            CleanExitMarker.ConsumeSessionMarks(ProcessId, Session);
            SessionState.EraseInt("TrainFatalExit_Notices");
            SessionState.EraseInt("TrainFatalExit_Flushes");
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            LogAssert.ignoreFailingMessages = false;
        }

        // Test Runnerへ期待する遷移を宣言し、実起動失敗callback自身に終了させる。
        // Declare the expected transition to Test Runner and let the real startup-failure callback perform the exit.
        private sealed class FailedSnapshotExit : IEditModeTestYieldInstruction
        {
            public bool ExpectDomainReload => false;
            public bool ExpectedPlaymodeState => false;

            public IEnumerator Perform()
            {
                GameShutdownEvent.ResetForNewSession();
                SessionState.SetInt("TrainFatalExit_Notices", 0);
                SessionState.SetInt("TrainFatalExit_Flushes", 0);
                CleanExitMarkWriter.InstallAtStartup(ProcessId, Session);
                GameShutdownEvent.RegisterParticipant(new SaveProbe());
                using var notification = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
                {
                    Assert.AreEqual(GameShutdownReason.FatalSynchronizationFailure, reason);
                    SessionState.SetInt("TrainFatalExit_Notices", SessionState.GetInt("TrainFatalExit_Notices", 0) + 1);
                });
                using var client = new TrainSnapshotClientFixture();
                LogAssert.Expect(LogType.Error, new Regex("TrainFullSnapshot.*initial apply failed"));
                client.ApplyRail(new byte[] { 0xC1 });
                var failure = Assert.Throws<TrainInitialSnapshotException>(() => client.Handler.WaitForInitialApplyAsync().GetAwaiter().GetResult());
                var callback = typeof(InitializeScenePipeline).GetMethod("OnMainGameInitializationFailed", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(callback);
                callback.Invoke(null, new object[] { failure });
                Assert.IsFalse(GameShutdownEvent.NotifyUnannouncedExit());
                while (EditorApplication.isPlaying) yield return null;
            }
        }

        private sealed class SaveProbe : IGameShutdownParticipant
        {
            public Cysharp.Threading.Tasks.UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                SessionState.SetInt("TrainFatalExit_Flushes", SessionState.GetInt("TrainFatalExit_Flushes", 0) + 1);
                return Cysharp.Threading.Tasks.UniTask.FromResult(ShutdownFlushResult.Flushed);
            }
        }
    }
}
