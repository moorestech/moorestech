using Client.Game.Common;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

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

            var shutdown = GameShutdownEvent.FireGameShutdownAsync();
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

            var shutdown = GameShutdownEvent.FireGameShutdownAsync();
            participant.Complete(ShutdownFlushResult.Flushed);
            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown.GetAwaiter().GetResult());

            var secondShutdown = GameShutdownEvent.FireGameShutdownAsync();
            Assert.AreEqual(ShutdownFlushResult.AlreadyShutdown, secondShutdown.GetAwaiter().GetResult());
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
