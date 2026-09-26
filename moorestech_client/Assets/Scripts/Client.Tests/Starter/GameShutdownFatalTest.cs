using System.Reflection;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Starter
{
    public class GameShutdownFatalTest
    {
        private const int ProcessId = 999002;
        private const string Session = "fatal_shutdown_test";

        [SetUp]
        [TearDown]
        public void Reset()
        {
            GameShutdownEvent.ResetForNewSession();
            CleanExitMarker.ConsumeSessionMarks(ProcessId, Session);
        }

        [Test]
        public void FatalQuit_BypassesParticipantsDeferralAndCleanMarks_AndIsIdempotent()
        {
            CleanExitMarkWriter.InstallAtStartup(ProcessId, Session);
            var participant = new SaveParticipant();
            GameShutdownEvent.RegisterParticipant(participant);
            // 実modeの保存参加者にも到達しない。到達時のnull依存Errorは通常検出する。
            // The actual mode-specific save participants must stay untouched; null-dependency errors remain detectable.
            GameShutdownEvent.RegisterParticipant(new Client.Starter.Initialization.RemoteServerSaveFlushParticipant(null));
            GameShutdownEvent.RegisterParticipant(new Client.Starter.Initialization.EmbeddedServerShutdownParticipant(null));
            var notices = 0;
            var flushes = 0;
            using var notice = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
            {
                Assert.AreEqual(GameShutdownReason.FatalSynchronizationFailure, reason);
                notices++;
            });
            using var flush = GameShutdownEvent.OnShutdownFlushed.Subscribe(_ => flushes++);
            GameShutdownEvent.QuitAfterSynchronizationFailure();
            GameShutdownEvent.QuitAfterSynchronizationFailure();
            Assert.IsFalse(GameShutdownEvent.NotifyUnannouncedExit());
            Assert.IsTrue((bool)typeof(GameShutdownEvent).GetMethod("OnApplicationWantsToQuit", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
            Assert.AreEqual(0, participant.Calls);
            Assert.AreEqual(1, notices);
            Assert.AreEqual(0, flushes);
            var marks = CleanExitMarker.ConsumeSessionMarks(ProcessId, Session);
            Assert.IsFalse(marks.ExitedCleanly);
            Assert.IsFalse(marks.ShutdownStalled);
        }

        [Test]
        public void NewSessionAfterFatal_RestoresNormalSavingAndQuitFlags()
        {
            GameShutdownEvent.QuitAfterSynchronizationFailure();
            GameShutdownEvent.ResetForNewSession();
            foreach (var name in new[] { "_quitAllowed", "_quitInProgress", "_fired", "_fatal" })
                Assert.IsFalse((bool)typeof(GameShutdownEvent).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), name);
            CleanExitMarkWriter.InstallAtStartup(ProcessId, Session);
            var participant = new SaveParticipant();
            GameShutdownEvent.RegisterParticipant(participant);
            Assert.AreEqual(ShutdownFlushResult.Flushed, GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit).GetAwaiter().GetResult());
            Assert.AreEqual(1, participant.Calls);
            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(ProcessId, Session).ExitedCleanly);
        }

        private sealed class SaveParticipant : IGameShutdownParticipant
        {
            public int Calls;
            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                Calls++;
                return UniTask.FromResult(ShutdownFlushResult.Flushed);
            }
        }
    }
}
