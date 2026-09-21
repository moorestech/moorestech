using System;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Starter
{
    // 終了時のflush待ちが「参加者の完了まで待つ」「上限到達を完了と畳まない」ことを固定する
    // Pins that the shutdown flush waits for participants and never folds a timeout into completion
    public class GameShutdownFlushTest
    {
        [Test]
        public void FireGameShutdownAsync_WaitsForParticipantAndKeepsTimeoutDistinct()
        {
            GameShutdownEvent.ResetForNewSession();
            var participant = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);

            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            Assert.AreEqual(UniTaskStatus.Pending, shutdown.Status);

            participant.Complete(ShutdownFlushResult.FlushTimedOut);
            Assert.AreEqual(ShutdownFlushResult.FlushTimedOut, shutdown.GetAwaiter().GetResult());
        }

        [Test]
        public void FireGameShutdownAsync_SecondFireIsAlreadyShutdown()
        {
            GameShutdownEvent.ResetForNewSession();
            var participant = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);

            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            participant.Complete(ShutdownFlushResult.Flushed);
            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown.GetAwaiter().GetResult());

            var secondShutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            Assert.AreEqual(ShutdownFlushResult.AlreadyShutdown, secondShutdown.GetAwaiter().GetResult());
        }

        [Test]
        public void NotifyUnannouncedExit_FiresUnawaitableExitWhenNobodyDeclared()
        {
            GameShutdownEvent.ResetForNewSession();
            var notifiedReason = GameShutdownReason.IntentionalExit;
            var notifiedCount = 0;
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
            {
                notifiedReason = reason;
                notifiedCount++;
            });

            Assert.IsTrue(GameShutdownEvent.NotifyUnannouncedExit(), "意思表明の無い終了で保険が発火していない");
            Assert.AreEqual(1, notifiedCount);
            Assert.AreEqual(GameShutdownReason.UnawaitableExit, notifiedReason);
        }

        [Test]
        public void NotifyUnannouncedExit_DoesNothingAfterCanonicalExitDeclared()
        {
            GameShutdownEvent.ResetForNewSession();
            var notifiedCount = 0;
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(_ => notifiedCount++);

            var participant = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);
            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);

            // 意思表明済みの終了では保険は何もしない。ここで発火すると終了処理中の停止が正常終了に化ける
            // The fallback stays silent once the exit was declared; firing here would disguise a stall during shutdown as a clean exit
            Assert.IsFalse(GameShutdownEvent.NotifyUnannouncedExit(), "意思表明済みなのに保険が二重発火している");
            Assert.AreEqual(1, notifiedCount);

            participant.Complete(ShutdownFlushResult.Flushed);
            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown.GetAwaiter().GetResult());
        }

        // 完了タイミングをテストが握る参加者。PlayerLoop無しのEditModeでも決定的に進められる
        // A participant whose completion the test drives, so EditMode without a PlayerLoop stays deterministic
        private class ControllableShutdownParticipant : IGameShutdownParticipant
        {
            private readonly UniTaskCompletionSource<ShutdownFlushResult> _flushCompletionSource = new();

            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                return _flushCompletionSource.Task;
            }

            public void Complete(ShutdownFlushResult flushResult)
            {
                _flushCompletionSource.TrySetResult(flushResult);
            }
        }
    }
}
