using System.Collections.Generic;
using NUnit.Framework;
using Server.Boot.Loop;

namespace Tests.UnitTest.Server
{
    // キューの寿命管理の純粋テスト。PlayModeを要らないためServer.Tests側に置く
    // Pure lifetime-management tests for the queue; kept in Server.Tests since they need no PlayMode
    public class ServerThreadActionQueueTest
    {
        private sealed class RecordingAction : IServerThreadAction
        {
            internal int RunCount;
            internal int StoppedCount;

            public void Run()
            {
                RunCount++;
            }

            public void OnServerStopped()
            {
                StoppedCount++;
            }
        }

        [Test]
        public void サーバー終了は待機処理を失敗通知して受付を閉じる()
        {
            var queue = new ServerThreadActionQueue();
            Assert.IsFalse(queue.HasDrainedThisLifetime, "tick末尾が動く前は受付を開かない");
            Assert.IsFalse(queue.TryEnqueue(new RecordingAction()), "tick末尾が動く前の投入は受け付けない");

            queue = NewDrainedQueue();
            var action = new RecordingAction();
            Assert.IsTrue(queue.TryEnqueue(action));
            queue.Stop();

            Assert.AreEqual(0, action.RunCount);
            Assert.AreEqual(1, action.StoppedCount);
            Assert.IsFalse(queue.HasDrainedThisLifetime);
            Assert.IsFalse(queue.TryEnqueue(action));
        }

        // 旧サーバーのStopは自分のインスタンスだけを閉じる。新サーバーの受付には届かない
        // An old server's Stop closes only its own instance and never reaches a newer server's admission
        [Test]
        public void 旧サーバーの終了は新サーバーの処理を捨てない()
        {
            var oldQueue = NewDrainedQueue();
            oldQueue.Stop();
            var queue = NewDrainedQueue();
            var action = new RecordingAction();
            Assert.IsTrue(queue.TryEnqueue(action));

            oldQueue.Stop();
            Assert.IsTrue(queue.HasDrainedThisLifetime);
            queue.Drain();
            Assert.AreEqual(1, action.RunCount);
            Assert.AreEqual(0, action.StoppedCount);
        }

        // 実行開始後も解放前ならStopが届く。開始済みの処理を停止通知なしで放置しない
        // Stop still reaches an action that started but has not been released, so no started work is left unnotified
        [Test]
        public void 開始済みで未解放の処理にも停止が届く()
        {
            var queue = NewDrainedQueue();
            var action = new RecordingAction();
            Assert.IsTrue(queue.TryEnqueue(action));
            queue.Drain();
            Assert.AreEqual(1, action.RunCount);

            queue.Stop();
            Assert.AreEqual(1, action.StoppedCount);
        }

        // 解放済みの処理へは停止を届けない。届けると完了済みの結果を停止で上書きしうる
        // A released action receives no stop; delivering one could overwrite an already finished outcome
        [Test]
        public void 解放済みの処理には停止が届かない()
        {
            var queue = NewDrainedQueue();
            var action = new RecordingAction();
            Assert.IsTrue(queue.TryEnqueue(action));
            queue.Drain();
            queue.Release(action);

            queue.Stop();
            Assert.AreEqual(0, action.StoppedCount);
        }

        // 次tickまでに解放されない処理はストールとして記録する。打ち切りはしない
        // Work not released by the next tick is recorded as a stall and never aborted
        [Test]
        public void 次tickまでに終わらない処理をストールとして記録する()
        {
            var queue = NewDrainedQueue();
            var action = new RecordingAction();
            Assert.IsTrue(queue.TryEnqueue(action));
            queue.Drain();
            Assert.IsFalse(queue.IsStalled);

            global::Core.Update.GameUpdater.RestoreCurrentTick(global::Core.Update.GameUpdater.CurrentTick + 1);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("tick末尾の処理が次tickまでに終わりませんでした"));
            queue.Drain();
            Assert.IsTrue(queue.IsStalled);
        }

        #region Internal

        // tick末尾が1度動いた状態のキューを作る。受付はそれまで開かない
        // Builds a queue whose tick end has run once; admission stays closed until then
        private static ServerThreadActionQueue NewDrainedQueue()
        {
            var queue = new ServerThreadActionQueue();
            queue.Drain();
            return queue;
        }

        #endregion
    }
}
