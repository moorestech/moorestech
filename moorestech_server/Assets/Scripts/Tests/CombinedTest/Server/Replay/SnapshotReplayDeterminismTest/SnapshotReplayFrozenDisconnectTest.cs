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
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.UnitTest.PlayerRiding;
using Tests.Util;
using Tests.Util.PlayerIdentity;

namespace Tests.CombinedTest.Server.Replay.SnapshotReplayDeterminismTest
{
    public class SnapshotReplayFrozenDisconnectTest
    {
        [Test]
        public void Freeze後の切断は次tickで記録され再生でも同じ席を選ぶ()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-replay-frozen-close-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var savePath = Path.Combine(root, "save.json");
            var directory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath);
            var (creator, provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) { worldDataDirectory = directory });
            var log = new ReceivedPacketLog();
            using var socket = new ReplayConnectionTestSocket();

            try
            {
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                GameUpdater.RestoreCurrentTick(0);
                var first = BoundPacketContext.Handshake(creator, "steam:1", out var firstId);
                var second = BoundPacketContext.Handshake(creator, "steam:2", out var secondId);
                var environment = new TrainTestEnvironment(provider, ServerContext.WorldBlockDatastore, creator);
                var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
                var riding = provider.GetRequiredService<IPlayerRidingDatastore>();
                riding.LoadSaveData(new List<PlayerRidingSaveData>
                {
                    new(firstId, RidableType.TrainCar.AsPrimitive(), car.TrainCarInstanceId.AsPrimitive().ToString(), 0),
                });
                File.WriteAllText(savePath, provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());

                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
                var ride = MessagePackSerializer.Serialize(new RideActionProtocol.RequestRideActionMessagePack(RideActionType.Ride, target));
                var dismount = MessagePackSerializer.Serialize(new RideActionProtocol.RequestRideActionMessagePack(RideActionType.Dismount, target));
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                var connections = (PlayerConnectionRegistry)provider.GetRequiredService<IPlayerConnectionChecker>();
                var events = provider.GetRequiredService<EventProtocolProvider>();
                var firstReceiver = socket.CreateReceiver(creator, first, queue, log);
                var secondReceiver = socket.CreateReceiver(creator, second, queue, log);
                log.Start(Path.Combine(root, "packets"), 1);

                // Freeze済みの処理中に切断を積み、同tickの後続乗車では旧接続を維持する
                // Enqueue the close during frozen processing, keeping the old connection for later packets in that tick
                firstReceiver.EnqueuePacket(ride);
                queue.Enqueue(new ScheduleDisconnectEntry(first, firstReceiver, queue, connections, events, log));
                secondReceiver.EnqueuePacket(ride);
                GameUpdater.Update();
                Assert.IsTrue(connections.IsConnected(firstId));
                Assert.IsTrue(riding.TryGetRidingState(secondId, out var firstTickState));
                Assert.AreEqual(1, firstTickState.SeatIndex);

                // 保留された切断が次tickの先頭で処理され、二人目はseat0へ移れる
                // The deferred close runs first next tick, allowing the second rider to move to seat 0
                secondReceiver.EnqueuePacket(dismount);
                secondReceiver.EnqueuePacket(ride);
                GameUpdater.Update();
                Assert.IsFalse(connections.IsConnected(firstId));
                Assert.IsTrue(riding.TryGetRidingState(secondId, out var secondTickState));
                Assert.AreEqual(0, secondTickState.SeatIndex);
                log.Stop();
                var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
                CollectionAssert.AreEqual(new ulong[] { 1, 1, 2, 2, 2 }, records.ConvertAll(record => record.Tick));
                CollectionAssert.AreEqual(new[] { ReceivedPacketRecordKind.Packet, ReceivedPacketRecordKind.Packet,
                    ReceivedPacketRecordKind.Disconnect, ReceivedPacketRecordKind.Packet, ReceivedPacketRecordKind.Packet },
                    records.ConvertAll(record => record.Kind));
                var expected = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, savePath, log.SegmentFilePaths(), 2));
                var comparison = SnapshotJsonComparer.Compare(expected, result.SnapshotJson);
                Assert.IsTrue(comparison.Equal, string.Join("\n", comparison.Differences));
            }
            finally
            {
                log.Stop();
                Directory.Delete(root, true);
            }
        }

        private sealed class ScheduleDisconnectEntry : ITickEndPacketEntry
        {
            private readonly PacketResponseContext _context;
            private readonly ReceiveQueueProcessor _receiver;
            private readonly TickEndPacketQueue _queue;
            private readonly PlayerConnectionRegistry _connections;
            private readonly EventProtocolProvider _events;
            private readonly ReceivedPacketLog _log;

            public bool IsActive => true;

            public ScheduleDisconnectEntry(PacketResponseContext context, ReceiveQueueProcessor receiver,
                TickEndPacketQueue queue, PlayerConnectionRegistry connections, EventProtocolProvider events,
                ReceivedPacketLog log)
            {
                _context = context;
                _receiver = receiver;
                _queue = queue;
                _connections = connections;
                _events = events;
                _log = log;
            }

            public void Process()
            {
                ConnectionDisconnectEntry.Schedule(_context, _receiver, _queue, _connections, _events, _log);
            }
        }
    }
}
