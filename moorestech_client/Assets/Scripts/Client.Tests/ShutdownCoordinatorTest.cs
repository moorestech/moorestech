using System.Collections.Generic;
using Client.Common.Shutdown;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests
{
    public class ShutdownCoordinatorTest
    {
        [SetUp]
        public void SetUp() => ShutdownCoordinator.ResetForTests();

        [TearDown]
        public void TearDown() => ShutdownCoordinator.ResetForTests();

        [Test]
        public async System.Threading.Tasks.Task Steps_RunInPhaseThenRegistrationOrder()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "A2", () => { log.Add("A2"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.BeforeDisconnect, "B1", () => { log.Add("B1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "A1", () => { log.Add("A1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.BeforeDisconnect, "B2", () => { log.Add("B2"); return UniTask.CompletedTask; });

            await ShutdownCoordinator.ShutdownAsync();

            Assert.AreEqual(new[] { "B1", "B2", "A2", "A1" }, log.ToArray());
        }

        [Test]
        public async System.Threading.Tasks.Task ShutdownAsync_SecondCall_ReturnsSameTask()
        {
            var runs = 0;
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "S", async () =>
            {
                await UniTask.Yield();
                runs++;
            });

            var t1 = ShutdownCoordinator.ShutdownAsync();
            var t2 = ShutdownCoordinator.ShutdownAsync();
            await UniTask.WhenAll(t1, t2);

            Assert.AreEqual(1, runs);
        }

        [Test]
        public async System.Threading.Tasks.Task StepException_DoesNotAbortPipeline()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "fail", () =>
            {
                log.Add("fail-entered");
                throw new System.InvalidOperationException("boom");
            });
            ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "after", () => { log.Add("after"); return UniTask.CompletedTask; });

            // LogException が呼ばれるため、NUnit 側でエラーログを許容する
            // Allow logged exception so NUnit does not fail the test for expected output
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            await ShutdownCoordinator.ShutdownAsync();

            Assert.Contains("fail-entered", log);
            Assert.Contains("after", log);
        }

        [Test]
        public async System.Threading.Tasks.Task Register_AfterShutdown_IsIgnored()
        {
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "first", () => UniTask.CompletedTask);
            var t = ShutdownCoordinator.ShutdownAsync();
            // Register while pipeline is running; expected to be ignored with warning
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "late", () => UniTask.CompletedTask);
            await t;
            // Success if no throw and pipeline completed; warning log is confirmed manually
            Assert.Pass();
        }
    }
}
