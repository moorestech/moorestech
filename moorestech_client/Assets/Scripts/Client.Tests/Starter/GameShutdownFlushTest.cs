using System.Collections;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void FireGameShutdownAsync_FailedParticipantMakesAggregateFlushFailed()
        {
            GameShutdownEvent.ResetForNewSession();
            var failed = new ControllableShutdownParticipant();
            var flushed = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(failed);
            GameShutdownEvent.RegisterParticipant(flushed);

            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            failed.Complete(ShutdownFlushResult.FlushFailed);
            flushed.Complete(ShutdownFlushResult.Flushed);

            Assert.AreEqual(ShutdownFlushResult.FlushFailed, shutdown.GetAwaiter().GetResult());
        }

        [Test]
        public void ResetForNewSession_DropsPendingParticipantSoFireDoesNotWait()
        {
            GameShutdownEvent.ResetForNewSession();
            GameShutdownEvent.RegisterParticipant(new ControllableShutdownParticipant());
            GameShutdownEvent.ResetForNewSession();

            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);

            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown.GetAwaiter().GetResult());
        }

        [Test]
        public void RegisterParticipant_SameParticipantTwiceIsFlushedOnce()
        {
            GameShutdownEvent.ResetForNewSession();
            var participant = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);
            GameShutdownEvent.RegisterParticipant(participant);

            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            participant.Complete(ShutdownFlushResult.Flushed);

            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown.GetAwaiter().GetResult());
            Assert.AreEqual(1, participant.FlushCallCount);
        }

        [UnityTest]
        public IEnumerator WantsToQuit_WithNoParticipantDuringLoad_AllowsQuitOnlyAfterCallbackReturns()
        {
            // ロード中（内蔵サーバーの参加者が未登録）の終了要求。書き出しが同期で終わっても、閉じる許可は wantsToQuit を抜けた後に出す
            // A quit request while loading (embedded server participant not yet registered); even when the flush completes synchronously, closing is allowed only after wantsToQuit returns
            GameShutdownEvent.ResetForNewSession();
            var firstRequestAllowed = ApplicationQuitGate.OnApplicationWantsToQuit();
            var allowedWithinSameCallback = ApplicationQuitGate.OnApplicationWantsToQuit();

            // 1フレーム後に終了口へ届き、Unity が出し直す終了要求は通る
            // A frame later the exit is reached, and the quit request Unity reissues passes
            var deadline = Time.realtimeSinceStartup + 5f;
            var allowedAfterFrames = false;
            while (!allowedAfterFrames && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                allowedAfterFrames = ApplicationQuitGate.OnApplicationWantsToQuit();
            }
            GameShutdownEvent.ResetForNewSession();

            Assert.IsFalse(firstRequestAllowed, "最初の終了要求を保留せずに通した");
            Assert.IsFalse(allowedWithinSameCallback, "フレームを跨がずに終了口まで進んだ（wantsToQuit の中で Application.Quit を呼ぶため取り消され、アプリが閉じない）");
            Assert.IsTrue(allowedAfterFrames, "保留した終了要求が5秒以内に終了口へ届かなかった（アプリが閉じない）");
        }

        // 完了タイミングをテストが握る参加者。PlayerLoop無しのEditModeでも決定的に進められる
        // A participant whose completion the test drives, so EditMode without a PlayerLoop stays deterministic
        private class ControllableShutdownParticipant : IGameShutdownParticipant
        {
            private readonly UniTaskCompletionSource<ShutdownFlushResult> _flushCompletionSource = new();
            public int FlushCallCount { get; private set; }

            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                FlushCallCount++;
                return _flushCompletionSource.Task;
            }

            public void Complete(ShutdownFlushResult flushResult)
            {
                _flushCompletionSource.TrySetResult(flushResult);
            }
        }
    }
}
