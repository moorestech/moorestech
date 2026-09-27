using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Loop;
using UnityEngine.TestTools;
using System.Collections;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecRunnerTest
    {
        [SetUp]
        public void ResetServerQueue()
        {
            ServerThreadActionQueue.Stop();
        }

        [Test]
        public void サーバー終了は待機処理を失敗通知して受付を閉じる()
        {
            ServerThreadActionQueue.ResetForNewServer();
            ServerThreadActionQueue.Drain();
            Assert.IsFalse(ServerThreadActionQueue.HasDrainedThisLifetime, "更新スレッド開始前のtickは有効化しない");

            ServerThreadActionQueue.BeginServerThread();
            ServerThreadActionQueue.Drain();
            var ran = false;
            var stopped = false;
            Assert.IsTrue(ServerThreadActionQueue.TryEnqueue(() => ran = true, () => stopped = true));
            ServerThreadActionQueue.Stop();

            Assert.IsFalse(ran);
            Assert.IsTrue(stopped);
            Assert.IsFalse(ServerThreadActionQueue.HasDrainedThisLifetime);
            Assert.IsFalse(ServerThreadActionQueue.TryEnqueue(() => ran = true, () => stopped = true));
        }

        [UnityTest]
        public IEnumerator コンパイルエラーは実行せず返る() => UniTask.ToCoroutine(async () =>
        {
            var result = await RemoteExecRunner.RunAsync("return 1 +;", RemoteExecTarget.Client);
            Assert.IsFalse(result.Ok);
            Assert.IsNotEmpty(result.CompileErrors);
        });

        [UnityTest]
        public IEnumerator サーバー未起動は即時失敗する() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(UnityEngine.LogType.Warning, "内蔵サーバーが起動していないため、サーバー側では実行できません");
            var result = await RemoteExecRunner.RunAsync("return 1;", RemoteExecTarget.Server);
            Assert.IsFalse(result.Ok);
            Assert.That(result.Exception, Does.Contain("サーバー"));
        });
    }
}
