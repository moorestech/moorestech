using System;
using System.Reflection;
using System.Threading;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Loop;
using UnityEngine.TestTools;

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
            using var invocation = new RemoteExecServerInvocation(Entry(), cancellation.Token, new ServerThreadActionQueue());
            var completion = invocation.Completion;
            cancellation.Cancel();
            invocation.Run();

            Assert.AreEqual(0, _started);
            Assert.AreEqual(UniTaskStatus.Canceled, completion.Status);
        }

        [Test]
        public void 開始後の切断では本体完了まで待つ()
        {
            using var cancellation = new CancellationTokenSource();
            using var invocation = new RemoteExecServerInvocation(Entry(), cancellation.Token, new ServerThreadActionQueue());
            var completion = invocation.Completion;
            invocation.Run();
            cancellation.Cancel();

            // 開始後に待機を打ち切ると、次の要求とログ捕捉が重なる
            // Abandoning the wait after start would overlap log capture with the next request
            Assert.AreEqual(1, _started);
            Assert.AreEqual(UniTaskStatus.Pending, completion.Status);
            _completion.TrySetResult("finished");
            Assert.AreEqual("finished", completion.GetAwaiter().GetResult());
        }

        [Test]
        public void サーバー停止で実行前に落ちた要求は専用の拒否理由を返す()
        {
            using var invocation = new RemoteExecServerInvocation(Entry(), CancellationToken.None, new ServerThreadActionQueue());
            var completion = invocation.Completion;

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("実行前の要求を拒否"));
            invocation.OnServerStopped();
            invocation.Run();

            Assert.AreEqual(0, _started);
            var error = Assert.Throws<RemoteExecServerStoppedBeforeStartException>(() => completion.GetAwaiter().GetResult());
            StringAssert.Contains("内蔵サーバーが終了", error.Message);
        }

        // 開始済みの実行は成功も失敗も名乗れない。待機者を放置せず結果不明で閉じる
        // A started run can claim neither success nor failure, so the waiter is closed as unknown rather than left hanging
        [Test]
        public void 開始済み要求への停止通知は結果不明として閉じる()
        {
            using var invocation = new RemoteExecServerInvocation(Entry(), CancellationToken.None, new ServerThreadActionQueue());
            var completion = invocation.Completion;

            invocation.Run();
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("結果不明で閉じました"));
            invocation.OnServerStopped();

            Assert.AreEqual(1, _started);
            var error = Assert.Throws<RemoteExecServerAbandonedException>(() => completion.GetAwaiter().GetResult());
            StringAssert.Contains("結果は不明", error.Message);
        }

        // 結果不明で閉じた後に本体が完了しても、その結果で上書きしない
        // A body that finishes after the unknown close never overwrites that outcome
        [Test]
        public void 結果不明で閉じた後の本体完了は結果を上書きしない()
        {
            using var invocation = new RemoteExecServerInvocation(Entry(), CancellationToken.None, new ServerThreadActionQueue());
            var completion = invocation.Completion;

            invocation.Run();
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("結果不明で閉じました"));
            invocation.OnServerStopped();
            _completion.TrySetResult("finished");

            Assert.Throws<RemoteExecServerAbandonedException>(() => completion.GetAwaiter().GetResult());
        }

        private static MethodInfo Entry()
        {
            return typeof(RemoteExecServerInvocationTest).GetMethod(nameof(Invoke), BindingFlags.NonPublic | BindingFlags.Static);
        }

        // 本体の入口は送信コードと同じ形（CancellationToken 1引数）で受ける
        // The entry takes the same shape as submitted code: one CancellationToken parameter
        private static UniTask<object> Invoke(CancellationToken cancellationToken)
        {
            _started++;
            return _completion.Task;
        }
    }
}
