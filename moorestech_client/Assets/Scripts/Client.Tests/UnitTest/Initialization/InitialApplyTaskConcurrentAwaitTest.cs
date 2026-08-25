using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.UnitTest.Initialization
{
    /// <summary>
    ///     初期適用タスクの保持形（UniTask→Task）が未完了中の同時待機を支え、失敗を全待機へ伝えることを固定する。
    ///     起動時はInitialEventApplyWaiterと後着生成の2箇所が同じ近傍タスクを同時にawaitするため、この性質が崩れると起動が止まる。
    ///     Locks the retention form of the initial-apply task (UniTask→Task): it backs concurrent awaits while pending and reports failure to every awaiter.
    ///     At startup InitialEventApplyWaiter and the background stream await the same near-field task at once, so losing this property halts startup.
    /// </summary>
    public class InitialApplyTaskConcurrentAwaitTest
    {
        [Test]
        public void 未完了の保持タスクへ待機が2本付いても両方へ完了が届く()
        {
            var nearFieldSource = new UniTaskCompletionSource();
            var retained = nearFieldSource.Task.AsTask();

            // 未完了のうちに2本目を登録するのが起動時の実状況。ExecuteSynchronouslyで完了通知を同期観測する
            // Registering the second awaiter while pending is the real startup situation; ExecuteSynchronously observes completion synchronously
            var completedCount = 0;
            retained.ContinueWith(_ => completedCount++, TaskContinuationOptions.ExecuteSynchronously);
            retained.ContinueWith(_ => completedCount++, TaskContinuationOptions.ExecuteSynchronously);
            Assert.AreEqual(0, completedCount, "未完了なのに完了が届いている");

            nearFieldSource.TrySetResult();
            Assert.AreEqual(2, completedCount, "同時待機の一方へ完了が届いていない");
        }

        [Test]
        public void 近傍生成の失敗は全ての待機へ伝播する()
        {
            var nearFieldSource = new UniTaskCompletionSource();
            var retained = nearFieldSource.Task.AsTask();

            var faultedCount = 0;
            retained.ContinueWith(task => faultedCount += task.IsFaulted ? 1 : 0, TaskContinuationOptions.ExecuteSynchronously);
            retained.ContinueWith(task => faultedCount += task.IsFaulted ? 1 : 0, TaskContinuationOptions.ExecuteSynchronously);

            nearFieldSource.TrySetException(new InvalidOperationException("near field failed"));

            Assert.AreEqual(2, faultedCount, "失敗が一方の待機へ伝播していない");
            Assert.AreEqual("near field failed", retained.Exception.InnerException.Message);
        }
    }
}
