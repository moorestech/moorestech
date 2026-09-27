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

        // 世代管理そのものの純粋テストは Server.Tests 側の ServerThreadActionQueueTest へ移した（Server.Boot が Client.Tests へ internal を開く必要をなくすため）
        // The pure generation-management tests moved to Server.Tests' ServerThreadActionQueueTest, so Server.Boot no longer needs to expose internals to Client.Tests

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
