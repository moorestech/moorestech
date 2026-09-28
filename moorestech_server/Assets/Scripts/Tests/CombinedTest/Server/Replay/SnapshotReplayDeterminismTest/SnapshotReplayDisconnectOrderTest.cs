using System;
using System.Collections.Generic;
using System.IO;
using Core.Update;
using Game.Context;
using Game.Paths;
using Game.PlayerConnection;
using Game.PlayerRiding.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Replay;
using Server.Event;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Handshake;
using Tests.Module.TestMod;
using Tests.UnitTest.PlayerRiding;
using Tests.Util;
using Tests.Util.PlayerIdentity;

namespace Tests.CombinedTest.Server.Replay.SnapshotReplayDeterminismTest
{
    public class SnapshotReplayDisconnectOrderTest
    {
        [Test]
        public void 同tickの乗車要求は切断前の接続集合で判定する()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-replay-disconnect-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var savePath = Path.Combine(root, "save.json");
            var directory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = directory,
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var log = new ReceivedPacketLog();

            try
            {
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                GameUpdater.RestoreCurrentTick(0);
                var first = BoundPacketContext.Handshake(packet, "steam:1", out var firstId);
                var second = BoundPacketContext.Handshake(packet, "steam:2", out var secondId);
                var environment = new TrainTestEnvironment(provider, ServerContext.WorldBlockDatastore, packet);
                var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
                var riding = provider.GetRequiredService<IPlayerRidingDatastore>();
                riding.LoadSaveData(new List<PlayerRidingSaveData>
                {
                    new(firstId, RidableType.TrainCar.AsPrimitive(), car.TrainCarInstanceId.AsPrimitive().ToString(), 0),
                });
                File.WriteAllText(savePath, provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());

                // 一人目の要求で再生時の接続を復元し、二人目の乗車を同tick末尾で処理する
                // Restore the first connection through its request, then process the second ride at the same tick end
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
                var ride = MessagePackSerializer.Serialize(new RideActionProtocol.RequestRideActionMessagePack(RideActionType.Ride, target));
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                log.Start(Path.Combine(root, "packets"), 1);
                log.Append(1, firstId, ride);
                queue.Enqueue(new ReplayPacketEntry(packet, first, ride));
                log.Append(1, secondId, ride);
                queue.Enqueue(new ReplayPacketEntry(packet, second, ride));
                GameUpdater.Update();
                Assert.IsTrue(riding.TryGetRidingState(secondId, out var liveState));
                Assert.AreEqual(1, liveState.SeatIndex);

                // 本番の切断はtick処理後の待ち時間に起き、完了済みtickで記録される
                // A live disconnect occurs in the wait after tick processing and records that completed tick
                var connections = (PlayerConnectionRegistry)provider.GetRequiredService<IPlayerConnectionChecker>();
                var events = provider.GetRequiredService<EventProtocolProvider>();
                PlayerConnectionBinding.Unregister(firstId, first.EventSink, connections, events);
                log.AppendDisconnect(GameUpdater.CurrentTick, firstId);
                log.Stop();
                var expected = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, savePath, log.SegmentFilePaths(), 1));
                var comparison = SnapshotJsonComparer.Compare(expected, result.SnapshotJson);
                Assert.IsTrue(comparison.Equal, string.Join("\n", comparison.Differences));
            }
            finally
            {
                log.Stop();
                Directory.Delete(root, true);
            }
        }
    }
}
