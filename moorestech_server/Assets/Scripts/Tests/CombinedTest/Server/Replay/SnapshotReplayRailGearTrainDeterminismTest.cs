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
    // レール・歯車・列車はロード経路でID採番のため乱数を引く。再開時の乱数列がセーブ時と違えば以降の全状態がずれる
    // Rails, gears, and trains draw randomness for id allocation while loading; a different stream on resume shifts every later state
    public class SnapshotReplayRailGearTrainDeterminismTest
    {
        [Test]
        public void レールと歯車と列車を含む世界でもスナップショットkから再生するとk_plus_1と一致する()
        {
            var saveRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-{Guid.NewGuid():N}");
            var savePath = Path.Combine(saveRoot, "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            var packetLog = provider.GetRequiredService<ReceivedPacketLog>();

            try
            {
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

                GameRandom.Reseed(2026UL);
                GameUpdater.RestoreCurrentTick(0);
                ring.Start(10, 30, 16);
                SnapshotReplayRailGearTrainFixture.BuildWorld(provider, packet);

                for (var tick = 1; tick <= 25; tick++) GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
                GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
                CollectionAssert.AreEqual(new[] { 10UL, 20UL }.Select(WorldDataDirectory.SnapshotFileName).ToArray(), WorldDataDirectory.EnumerateSnapshotFiles(directory.SnapshotDirectory).Select(Path.GetFileName).ToArray());

                var expected20 = File.ReadAllText(directory.SnapshotFilePath(20));
                SnapshotReplayRailGearTrainFixture.AssertRailGearTrainPresent(expected20);
                var segments = packetLog.SegmentFilePaths().ToList();

                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory, directory.SnapshotFilePath(10), segments, 20));
                Assert.AreEqual(10UL, result.LoadedTick);
                Assert.AreEqual(20UL, result.ReachedTick);
                var comparison = SnapshotJsonComparer.Compare(expected20, result.SnapshotJson);
                Assert.IsTrue(comparison.Equal, "10→20 が一致しない:\n" + string.Join("\n", comparison.Differences));

                // 再開時の乱数列がセーブ時と一致していること。ここがずれると以降のID採番と確率判定が全部ずれる
                // The random stream on resume must match the save; a mismatch shifts every later id allocation and probability roll
                var expectedRandomState = SnapshotRandomState(expected20);
                CollectionAssert.AreEqual(expectedRandomState, SnapshotRandomState(result.SnapshotJson), "再生後の乱数状態がセーブ時と一致しない");
            }
            finally
            {
                packetLog.Stop();
                Directory.Delete(saveRoot, true);
            }
        }

        private static ulong[] SnapshotRandomState(string snapshotJson)
        {
            return JObject.Parse(snapshotJson)["randomState"].Select(token => token.ToObject<ulong>()).ToArray();
        }
    }
}
