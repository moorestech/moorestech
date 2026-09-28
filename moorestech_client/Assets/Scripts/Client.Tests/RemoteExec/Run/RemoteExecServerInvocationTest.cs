using System.Reflection;
using System.Threading;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.RemoteExec.Run
{
    public sealed class RemoteExecServerInvocationTest
    {
        private static UniTaskCompletionSource<object> _completion;
        private static int _started;

        [SetUp]
        public void SetUp()
        {
            _completion = new UniTaskCompletionSource<object>();
            _started = 0;
        }

        [Test]
        public void tick待機中の切断は本体を開始せず完了する()
        {
            using var cancellation = new CancellationTokenSource();
            using var invocation = new RemoteExecServerInvocation(Entry(), cancellation.Token);
            var completion = invocation.Completion;
            cancellation.Cancel();
            invocation.StartOnServerThread();

            Assert.AreEqual(0, _started);
            Assert.AreEqual(UniTaskStatus.Canceled, completion.Status);
        }

        [Test]
        public void 開始後の切断では本体完了まで待つ()
        {
            using var cancellation = new CancellationTokenSource();
            using var invocation = new RemoteExecServerInvocation(Entry(), cancellation.Token);
            var completion = invocation.Completion;
            invocation.StartOnServerThread();
            cancellation.Cancel();

            // 開始後に待機を打ち切ると、次の要求とログ捕捉が重なる
            // Abandoning the wait after start would overlap log capture with the next request
            Assert.AreEqual(1, _started);
            Assert.AreEqual(UniTaskStatus.Pending, completion.Status);
            _completion.TrySetResult("finished");
            Assert.AreEqual("finished", completion.GetAwaiter().GetResult());
        }

        private static MethodInfo Entry()
        {
            return typeof(RemoteExecServerInvocationTest).GetMethod(nameof(Invoke), BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static UniTask<object> Invoke()
        {
            _started++;
            return _completion.Task;
        }
    }
}
