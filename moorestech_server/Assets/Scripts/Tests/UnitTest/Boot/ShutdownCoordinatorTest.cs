using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Shutdown;

namespace Tests.UnitTest.Boot
{
    public class ShutdownCoordinatorTest
    {
        private bool _savedIgnoreFailingMessages;

        [SetUp]
        public void SetUp()
        {
            ShutdownCoordinator.ResetForTests();
            _savedIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
        }

        [TearDown]
        public void TearDown()
        {
            ShutdownCoordinator.ResetForTests();
            // テスト間で LogAssert の設定が漏れないよう元に戻す
            // Restore LogAssert setting so the flag does not leak to subsequent tests
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = _savedIgnoreFailingMessages;
        }

        [Test]
        public async System.Threading.Tasks.Task Steps_RunInPhaseThenRegistrationOrder()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "D1", () => { log.Add("D1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.StopAcceptingConnections, "S1", () => { log.Add("S1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "U1", () => { log.Add("U1"); return UniTask.CompletedTask; });

            await ShutdownCoordinator.ShutdownAsync();

            Assert.AreEqual(new[] { "S1", "U1", "D1" }, log.ToArray());
        }

        [Test]
        public async System.Threading.Tasks.Task ShutdownAsync_SecondCall_ReturnsSameTask()
        {
            var runs = 0;
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "S", async () => { await UniTask.Yield(); runs++; });
            var t1 = ShutdownCoordinator.ShutdownAsync();
            var t2 = ShutdownCoordinator.ShutdownAsync();
            await UniTask.WhenAll(t1, t2);
            Assert.AreEqual(1, runs);
        }

        [Test]
        public async System.Threading.Tasks.Task StepException_DoesNotAbortPipeline()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "fail", () => throw new System.InvalidOperationException("boom"));
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "after", () => { log.Add("after"); return UniTask.CompletedTask; });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            await ShutdownCoordinator.ShutdownAsync();
            Assert.Contains("after", log);
        }
    }
}
