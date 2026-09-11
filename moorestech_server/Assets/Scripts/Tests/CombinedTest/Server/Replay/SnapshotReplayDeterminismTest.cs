using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Replay;
using Server.Protocol;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.Replay
{
    // スナップショットkからパケットを流し直すとk+1と一致する。これが再生の忠実性の唯一の検査
    // Replaying packets from snapshot k must reproduce snapshot k+1; this is the only fidelity check for replay
    public class SnapshotReplayDeterminismTest
    {
        [Test]
        public void スナップショットkから再生するとk_plus_1と一致する()
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
                // 再生側と同じ初期化経路を通す。ここを省くと初期チャレンジ未登録の世界になり、再生側のロードだけが初期化してしまう
                // Take the same initialization path as replay; skipping it leaves challenges unregistered and only the replay side seeds them
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

                GameRandom.Reseed(2026UL);
                GameUpdater.RestoreCurrentTick(0);
                ring.Start(10, 4);
                GrantRequiredItems(provider, ForUnitTestModBlockId.BlockId, 3);
                GrantRequiredItems(provider, ForUnitTestModBlockId.ChestId, 1);
                UnlockBlock(provider, ForUnitTestModBlockId.ChestId);

                // tick末尾で処理される経路（ログ点）を通すため、受信プロセッサ相当の処理をtick中に行う
                // Route packets through the tick-end path (the log point), as the receive processor would
                var context = new PacketResponseContext(null);
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                void Send(byte[] payload) => queue.Enqueue(new ReplayPacketEntry(packet, context, payload, packetLog));

                for (var tick = 1; tick <= 45; tick++)
                {
                    if (tick == 3) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0)));
                    if (tick == 12) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.ChestId, (14, 0)));
                    if (tick == 12) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (12, 0)));
                    if (tick == 27) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (16, 0)));
                    GameUpdater.UpdateOneTick();
                }
                ring.WaitForPendingWrites();
                GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
                CollectionAssert.AreEqual(new ulong[] { 10, 20, 30, 40 }, ring.CopyWrittenTicks());

                var expected20 = File.ReadAllText(directory.SnapshotFilePath(20));
                var expected40 = File.ReadAllText(directory.SnapshotFilePath(40));
                var segments = packetLog.SegmentFilePaths().ToList();

                // 10→20（設置を跨ぐ）と 10→40（複数世代）を検査する
                // Check 10→20 (crossing placements) and 10→40 (spanning generations)
                var result20 = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory.SnapshotFilePath(10), segments, 20));
                Assert.AreEqual(10UL, result20.LoadedTick);
                Assert.AreEqual(20UL, result20.ReachedTick);
                Assert.AreEqual(2, result20.ReplayedPacketCount);
                var comparison20 = SnapshotJsonComparer.Compare(expected20, result20.SnapshotJson);
                Assert.IsTrue(comparison20.Equal, "10→20 が一致しない:\n" + string.Join("\n", comparison20.Differences));

                var result40 = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory.SnapshotFilePath(10), segments, 40));
                Assert.AreEqual(3, result40.ReplayedPacketCount);
                var comparison40 = SnapshotJsonComparer.Compare(expected40, result40.SnapshotJson);
                Assert.IsTrue(comparison40.Equal, "10→40 が一致しない:\n" + string.Join("\n", comparison40.Differences));
            }
            finally
            {
                // assert失敗時も書き込み中のFileStreamを解放してから一時ディレクトリを消す
                // Even on assert failure, release the open FileStream before deleting the temp directory
                packetLog.Stop();
                Directory.Delete(saveRoot, true);
            }
        }

        // ベルト搬送とレシピ加工が境界tickで進行中の世界でも再生が一致する。非決定性が最も出やすいのは毎tick変わる状態
        // Replay must also match when belt transport and recipe processing are mid-flight at the boundary ticks, where non-determinism is most likely
        [Test]
        public void 搬送中と加工中を含む世界でもスナップショットkから再生するとk_plus_1と一致する()
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
                ring.Start(10, 4);
                GrantRequiredItems(provider, ForUnitTestModBlockId.ChestId, 1);
                UnlockBlock(provider, ForUnitTestModBlockId.ChestId);
                SnapshotReplayWorldFixture.BuildMovingWorld();

                var context = new PacketResponseContext(null);
                var queue = provider.GetRequiredService<TickEndPacketQueue>();

                for (var tick = 1; tick <= 45; tick++)
                {
                    // 動的状態が動いている最中にパケットも1件挟み、パケット再生と物理進行の両方を同時に検査する
                    // Slip one packet in while the dynamic state is moving, so packet replay and physical progression are checked together
                    if (tick == 13) queue.Enqueue(new ReplayPacketEntry(packet, context, CreatePlaceBlockPayload(ForUnitTestModBlockId.ChestId, (20, 0)), packetLog));
                    GameUpdater.UpdateOneTick();
                }
                ring.WaitForPendingWrites();
                GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
                CollectionAssert.AreEqual(new ulong[] { 10, 20, 30, 40 }, ring.CopyWrittenTicks());

                var expected10 = File.ReadAllText(directory.SnapshotFilePath(10));
                var expected20 = File.ReadAllText(directory.SnapshotFilePath(20));
                var expected40 = File.ReadAllText(directory.SnapshotFilePath(40));
                var segments = packetLog.SegmentFilePaths().ToList();

                // フィクスチャが静的化したらここで落とす。動いていない世界の「一致」は検算になっていない
                // Fail here if the fixture goes static; "equal" on a frozen world proves nothing
                SnapshotReplayWorldFixture.AssertDynamicStatePresent(expected10);
                SnapshotReplayWorldFixture.AssertDynamicStatePresent(expected20);
                SnapshotReplayWorldFixture.AssertDynamicStateChanged(expected20, expected40);

                var result20 = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory.SnapshotFilePath(10), segments, 20));
                Assert.AreEqual(10UL, result20.LoadedTick);
                Assert.AreEqual(20UL, result20.ReachedTick);
                Assert.AreEqual(1, result20.ReplayedPacketCount);
                var comparison20 = SnapshotJsonComparer.Compare(expected20, result20.SnapshotJson);
                Assert.IsTrue(comparison20.Equal, "10→20 が一致しない:\n" + string.Join("\n", comparison20.Differences));

                var result40 = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory.SnapshotFilePath(10), segments, 40));
                Assert.AreEqual(1, result40.ReplayedPacketCount);
                var comparison40 = SnapshotJsonComparer.Compare(expected40, result40.SnapshotJson);
                Assert.IsTrue(comparison40.Equal, "10→40 が一致しない:\n" + string.Join("\n", comparison40.Differences));
            }
            finally
            {
                packetLog.Stop();
                Directory.Delete(saveRoot, true);
            }
        }
    }
}
