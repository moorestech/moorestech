using System;
using Client.Game.InGame.Map.MapObject;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Map.Instantiation
{
    public class MapObjectInstantiationCompletionTest
    {
        [Test]
        public void 成功時だけ完了フラグが立つ()
        {
            var completion = new MapObjectInstantiationCompletion();

            completion.Complete();

            Assert.IsTrue(completion.GetSuccessfulCompletionState().Value);
            Assert.AreEqual(UniTaskStatus.Succeeded, completion.WaitAsync().Status);
        }

        [Test]
        public void 失敗時は完了フラグを立てず待機をfaultさせる()
        {
            var completion = new MapObjectInstantiationCompletion();
            var waitTask = completion.WaitAsync();

            completion.Fail(new InvalidOperationException("failure"));

            Assert.IsFalse(completion.GetSuccessfulCompletionState().Value);
            Assert.AreEqual(UniTaskStatus.Faulted, waitTask.Status);
            Assert.Throws<InvalidOperationException>(() => waitTask.GetAwaiter().GetResult());
        }

        [Test]
        public void cancel時は完了フラグを立てず待機をcancelする()
        {
            var completion = new MapObjectInstantiationCompletion();
            var waitTask = completion.WaitAsync();

            completion.Cancel();

            Assert.IsFalse(completion.GetSuccessfulCompletionState().Value);
            Assert.AreEqual(UniTaskStatus.Canceled, waitTask.Status);
            Assert.Throws<OperationCanceledException>(() => waitTask.GetAwaiter().GetResult());
        }
    }
}
