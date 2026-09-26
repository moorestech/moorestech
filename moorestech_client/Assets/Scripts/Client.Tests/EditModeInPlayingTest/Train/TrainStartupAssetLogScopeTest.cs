using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest
{
    public class TrainStartupAssetLogScopeTest
    {
        private const string TreeMessage = "Tree prefab at index 1 is missing.";
        private const string DelayedFrame = "UnityEngine.ResourceManagement.Util.DelayedActionManager:LateUpdate ()";
        private const string ProviderFrame = "UnityEngine.ResourceManagement.ResourceProviders.AssetDatabaseProvider:LoadAssetAtPath (string,UnityEngine.ResourceManagement.ResourceProviders.ProvideHandle)";

        [TestCase(ProviderFrame + "\n" + DelayedFrame)]
        [TestCase(DelayedFrame + "\n\n")]
        public void KnownStartupLogs_AreConsumedInTheSameFrame(string stackTrace)
        {
            using var scope = new TrainStartupAssetLogScope();
            EmitCallback(TreeMessage, stackTrace, LogType.Error);
            EmitCallback("Tree prefab at index 6 is missing.", stackTrace, LogType.Error);

            // 実LogAssertの事後照合で可変回数とcallback順序を検証する。
            // Exercise real LogAssert matching after callbacks, including multiple logs per frame.
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(TreeMessage, "", LogType.Error)]
        [TestCase(TreeMessage, "Client.Game.Terrain:Load ()", LogType.Error)]
        [TestCase(TreeMessage, DelayedFrame + "\nClient.Game.Terrain:Load ()", LogType.Error)]
        [TestCase(TreeMessage, "UnityEngine.ResourceManagement.Util.DelayedActionManager:InternalLateUpdate (single)", LogType.Error)]
        [TestCase(TreeMessage, DelayedFrame, LogType.Warning)]
        [TestCase("Tree prefab at index 1 is missing. extra", DelayedFrame, LogType.Error)]
        [TestCase("[TrainFullSnapshot] initial apply failed", DelayedFrame, LogType.Error)]
        [TestCase("[TrainUnitHashVerifier] Hash mismatch detected.", DelayedFrame, LogType.Error)]
        [TestCase("Train delta apply failed", ProviderFrame, LogType.Error)]
        public void OtherLogs_RemainUnexpected(string message, string stackTrace, LogType type)
        {
            using var scope = new TrainStartupAssetLogScope();
            EmitCallback(message, stackTrace, type);
            AssertUnexpectedThenConsume(message, type);
        }

        [Test]
        public void DisposedStartupScope_DoesNotAcceptLaterTreeErrors()
        {
            var scope = new TrainStartupAssetLogScope();
            LogAssert.Expect(LogType.Log, "[TrainStartupAssetLogScope] Expected 0 terrain asset logs; original errors retained in Editor log.");
            scope.Dispose();
            LogAssert.NoUnexpectedReceived();
            EmitCallback(TreeMessage, DelayedFrame, LogType.Error);
            AssertUnexpectedThenConsume(TreeMessage, LogType.Error);
        }

        private static void AssertUnexpectedThenConsume(string message, LogType type)
        {
            var failure = Assert.Catch(() => LogAssert.NoUnexpectedReceived());
            StringAssert.Contains("Unhandled log message", failure.Message);
            StringAssert.Contains(message, failure.Message);

            // 拒否を確認してから注入ログだけを消費し、他テストへ残さない。
            // Consume the injected log only after proving rejection, leaving no residue for other tests.
            LogAssert.Expect(type, message);
            LogAssert.NoUnexpectedReceived();
        }

        private static void EmitCallback(string message, string stackTrace, LogType type)
        {
            // nativeログと同じ入口でmain/threaded購読とTestRunnerの両方へ渡す。
            // Use the native log callback boundary to reach both application subscribers and TestRunner.
            typeof(Application).GetMethod("CallLogCallback", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { message, stackTrace, type, true });
        }
    }
}
