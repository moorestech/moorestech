using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Starter.Initialization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.UnitTest.Initialization
{
    public class InitialEventApplyWaiterTest
    {
        [Test]
        public void 全対象が完了するまで待機は完了しない()
        {
            var first = new FakeWaitTarget();
            var second = new FakeWaitTarget();
            var waiting = InitialEventApplyWaiter.WaitAllAsync(new List<IInitialEventApplyWaitTarget> { first, second }).Preserve();

            Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
            first.Complete();
            Assert.AreEqual(UniTaskStatus.Pending, waiting.Status, "1本完了で待機が抜けている");
            second.Complete();
            Assert.AreEqual(UniTaskStatus.Succeeded, waiting.Status);
        }

        [Test]
        public void 対象の失敗は待機境界へ例外として届く()
        {
            var target = new FakeWaitTarget();
            var waiting = InitialEventApplyWaiter.WaitAllAsync(new List<IInitialEventApplyWaitTarget> { target }).Preserve();

            target.Fail(new InvalidOperationException("apply failed"));
            var thrown = Assert.Throws<InvalidOperationException>(() => waiting.GetAwaiter().GetResult());
            Assert.AreEqual("apply failed", thrown.Message);
        }

        private class FakeWaitTarget : IInitialEventApplyWaitTarget
        {
            private readonly UniTaskCompletionSource _completion = new();
            public UniTask WaitForInitialApplyAsync() => _completion.Task;
            public void Complete() => _completion.TrySetResult();
            public void Fail(Exception exception) => _completion.TrySetException(exception);
        }
    }
}
