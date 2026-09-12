using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Replay;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.Replay
{
    // 再生はバンドルに入っている世界のマップを読む。テンプレートを読むとマップオブジェクトが丸ごと入れ替わる
    // Replay reads the map of the world inside the bundle; reading the template swaps the whole map-object set
    public class SnapshotReplayWorldSourceTest
    {
        [Test]
        public void テンプレートと違うマップの世界でも0件のパケットで一致する()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-world-{Guid.NewGuid():N}");
            Directory.CreateDirectory(worldRoot);
            var directory = WorldDataDirectory.FromWorldRoot(worldRoot);
            WriteMapWithFewerMapObjects(directory.MapJsonFilePath);

            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = directory,
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            var packetLog = provider.GetRequiredService<ReceivedPacketLog>();

            try
            {
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

                GameRandom.Reseed(2026UL);
                GameUpdater.RestoreCurrentTick(0);
                ring.Start(10, 30, 16);
                for (var tick = 1; tick <= 25; tick++) GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
                GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();

                var expected20 = File.ReadAllText(directory.SnapshotFilePath(20));
                Assert.AreEqual(RetainedMapObjectCount, JObject.Parse(expected20)["mapObjects"].Children().Count(), "検査対象の世界がテンプレートと同じマップになっている");

                var segments = packetLog.SegmentFilePaths().ToList();
                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory, directory.SnapshotFilePath(10), segments, 20));
                var comparison = SnapshotJsonComparer.Compare(expected20, result.SnapshotJson);
                Assert.IsTrue(comparison.Equal, "バンドル内のマップではなくテンプレートを読んでいる:\n" + string.Join("\n", comparison.Differences));
            }
            finally
            {
                packetLog.Stop();
                Directory.Delete(worldRoot, true);
            }
        }

        // テンプレートのマップから一部のマップオブジェクトを落とした世界を書く。テンプレートを読む実装ならここで件数が食い違う
        // Write a world whose map drops some of the template's map objects, so an implementation reading the template disagrees on the count
        private static void WriteMapWithFewerMapObjects(string mapJsonFilePath)
        {
            var map = JObject.Parse(File.ReadAllText(WorldDataDirectory.ServerDataMapJsonPath(TestModDirectory.ForUnitTestModDirectory)));
            var mapObjects = (JArray)map["mapObjects"];
            Assert.Greater(mapObjects.Count, RetainedMapObjectCount, "テンプレートのマップオブジェクトが少なすぎて検査にならない");
            while (mapObjects.Count > RetainedMapObjectCount) mapObjects.RemoveAt(mapObjects.Count - 1);
            File.WriteAllText(mapJsonFilePath, map.ToString());
        }

        private const int RetainedMapObjectCount = 2;
    }
}
