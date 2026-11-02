using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.Shutdown
{
    // 最後のtickの後に届いた終了セーブも破棄経路で書き出す。tickだけに取り込みを任せると無言で消える
    // A shutdown save that lands after the final tick is still written by the teardown; leaving capture to the tick alone loses it silently
    public class ServerShutdownFlushesRequestedSaveTest
    {
        [Test]
        public void 最後のtickの後に要求したセーブも破棄で書き出される()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech-shutdown-save-{Guid.NewGuid():N}");
            var manager = new ServerInstanceManager(new[]
            {
                "--worldDirectory", worldRoot,
                "--mapMode", "template",
                "--port", "0",
                "--autoSave", "false",
                "--serverDataDirectory", TestModDirectory.ForUnitTestModDirectory,
            });

            try
            {
                manager.Start();

                // tickスレッドを先に止め、セーブ要求が最後のtickより後に届いた状態を決定的に作る（Unityの破棄がShutdownAsyncの待ちを追い越す経路と同じ）
                // Stop the tick thread first so the request deterministically lands after the final tick (the same as Unity teardown overtaking ShutdownAsync's wait)
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                ((CancellationTokenSource)typeof(ServerInstanceManager).GetField("_cancellationTokenSource", flags).GetValue(manager)).Cancel();
                var gameUpdate = (Thread)typeof(ServerInstanceManager).GetField("_gameUpdateThread", flags).GetValue(manager);
                Assert.IsTrue(gameUpdate.Join(TimeSpan.FromSeconds(5)), "ゲーム更新threadが止まらない");

                var saveJsonPath = WorldDataDirectory.FromWorldRoot(worldRoot).SaveJsonFilePath;
                if (File.Exists(saveJsonPath)) File.Delete(saveJsonPath);
                manager.RequestSave();
                manager.Dispose();

                Assert.IsFalse(manager.HasPendingSave, "破棄が終わっても終了セーブが書き出されないまま残っている");
                Assert.IsTrue(File.Exists(saveJsonPath), $"終了セーブのファイルが無い: {saveJsonPath}");
            }
            finally
            {
                manager.Dispose();
                if (Directory.Exists(worldRoot)) Directory.Delete(worldRoot, true);
            }
        }
    }
}
