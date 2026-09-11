using System;
using System.IO;
using System.Linq;
using System.Threading;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server
{
    // 常時記録が持つOSリソースは製品の終了経路で必ず手放す。残るとセッション毎にスレッドとハンドルが積み上がる
    // The production shutdown path must release the OS resources always-on capture holds; leftovers accumulate per session
    public class ServerShutdownReleasesCaptureTest
    {
        [Test]
        public void 終了経路が常時記録の区間ファイルのハンドルを手放す()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech-shutdown-{Guid.NewGuid():N}");
            var manager = new ServerInstanceManager(new[]
            {
                "--worldDirectory", worldRoot,
                "--mapMode", "template",
                "--port", "0",
                "--autoSave", "false",
                "--captureRing", "true",
                "--serverDataDirectory", TestModDirectory.ForUnitTestModDirectory,
            });

            try
            {
                manager.Start();
                var snapshotDirectory = WorldDataDirectory.FromWorldRoot(worldRoot).SnapshotDirectory;
                Assert.IsTrue(Directory.Exists(snapshotDirectory), "常時記録が開始されていない");

                manager.Dispose();

                // 開いたままのFileStreamが残っていれば排他オープンが失敗する。これが積み上がりの唯一の外形的な証拠
                // An exclusive open fails while a FileStream is still held; that is the only externally visible evidence of the leak
                var segments = Directory.GetFiles(snapshotDirectory, "packets_*.bin");
                Assert.IsNotEmpty(segments, "パケットログの区間ファイルが作られていない");
                foreach (var segment in segments)
                {
                    using var stream = new FileStream(segment, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Assert.IsNotNull(stream);
                }
            }
            finally
            {
                manager.Dispose();
                // 終了スレッドがディレクトリを離すのを少しだけ待ってから消す
                // Give the shutdown threads a moment to let go of the directory before deleting it
                Thread.Sleep(100);
                if (Directory.Exists(worldRoot)) Directory.Delete(worldRoot, true);
            }
        }
    }
}
