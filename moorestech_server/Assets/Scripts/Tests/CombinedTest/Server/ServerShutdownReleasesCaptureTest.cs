using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Core.Update;
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
        [TestCase(false)]
        [TestCase(true)]
        public void 終了経路が常時記録の区間ファイルのハンドルを手放す(bool waitForTick)
        {
            // 常時記録は既定で無効なので、本番のプレイ開始と同じく明示的に有効化してから起動する
            // Always-on capture is disabled by default, so it is enabled explicitly here just like the real play start
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());

            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech-shutdown-{Guid.NewGuid():N}");
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
                var connection = (Thread)typeof(ServerInstanceManager).GetField("_connectionUpdateThread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                var gameUpdate = (Thread)typeof(ServerInstanceManager).GetField("_gameUpdateThread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                var port = manager.BoundPort;
                if (waitForTick)
                {
                    var firstTick = GameUpdater.CurrentTick;
                    for (var attempt = 0; attempt < 500 && GameUpdater.CurrentTick == firstTick; attempt++) Thread.Sleep(10);
                    Assert.AreNotEqual(firstTick, GameUpdater.CurrentTick, "ゲーム更新が開始されていない");
                }
                var snapshotDirectory = WorldDataDirectory.FromWorldRoot(worldRoot).SnapshotDirectory;
                Assert.IsTrue(Directory.Exists(snapshotDirectory), "常時記録が開始されていない");

                manager.Dispose();
                Assert.IsFalse(connection.IsAlive);
                Assert.IsFalse(gameUpdate.IsAlive);
                using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Assert.DoesNotThrow(() => probe.Bind(new IPEndPoint(IPAddress.Any, port)));

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
                // 有効化を後続テストへ漏らさない。漏らすとテスト起動が無断で録り始める
                // The enabled decision must not leak into later tests, which would start recording unannounced
                AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());

                manager.Dispose();
                if (Directory.Exists(worldRoot)) Directory.Delete(worldRoot, true);
            }
        }
    }
}
