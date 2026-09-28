using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
using Tests.Module.TestMod;
using Tests.UnitTest.PlayerRiding;
using Tests.Util;
using Tests.Util.PlayerIdentity;

namespace Tests.CombinedTest.Server.Replay.SnapshotReplayDeterminismTest
{
    public class SnapshotReplayDisconnectOrderTest
    {
        [Test]
        public void 切断前と次tickの乗車要求が本番と再生で一致する()
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
            using var listener = CreateBoundLoopbackListener();
            using var clientSocket = ConnectTo(listener);
            using var acceptedSocket = listener.Accept();
            var sender = new SendQueueProcessor(acceptedSocket);

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
                var dismount = MessagePackSerializer.Serialize(new RideActionProtocol.RequestRideActionMessagePack(RideActionType.Dismount, target));
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                var connections = (PlayerConnectionRegistry)provider.GetRequiredService<IPlayerConnectionChecker>();
                var events = provider.GetRequiredService<EventProtocolProvider>();
                var firstReceiver = new ReceiveQueueProcessor(packet, sender, first, queue, log);
                var secondReceiver = new ReceiveQueueProcessor(packet, sender, second, queue, log);
                log.Start(Path.Combine(root, "packets"), 1);
                firstReceiver.EnqueuePacket(ride);
                secondReceiver.EnqueuePacket(ride);

                // 本番Cleanupと同じ入口で切断を積み、先行パケットより後で確定させる
                // Schedule through the production cleanup path, committing after preceding packets
                ConnectionDisconnectEntry.Schedule(first, firstReceiver, queue, connections, events, log);
                GameUpdater.Update();
                Assert.IsTrue(riding.TryGetRidingState(secondId, out var liveState));
                Assert.AreEqual(1, liveState.SeatIndex);
                Assert.IsFalse(connections.IsConnected(firstId));

                // 次tickに二人目が降車・再乗車すると、空いたseat0を使える
                // On the next tick, the second rider dismounts and reboards into the freed seat 0
                secondReceiver.EnqueuePacket(dismount);
                secondReceiver.EnqueuePacket(ride);
                GameUpdater.Update();
                Assert.IsTrue(riding.TryGetRidingState(secondId, out var nextState));
                Assert.AreEqual(0, nextState.SeatIndex);
                log.Stop();
                var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
                CollectionAssert.AreEqual(new[] { ReceivedPacketRecordKind.Packet, ReceivedPacketRecordKind.Packet,
                    ReceivedPacketRecordKind.Disconnect, ReceivedPacketRecordKind.Packet, ReceivedPacketRecordKind.Packet },
                    records.ConvertAll(record => record.Kind));
                CollectionAssert.AreEqual(new ulong[] { 1, 1, 1, 2, 2 }, records.ConvertAll(record => record.Tick));
                var expected = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, savePath, log.SegmentFilePaths(), 2));
                Assert.AreEqual(4, result.ReplayedPacketCount);
                var comparison = SnapshotJsonComparer.Compare(expected, result.SnapshotJson);
                Assert.IsTrue(comparison.Equal, string.Join("\n", comparison.Differences));
            }
            finally
            {
                sender.Dispose();
                log.Stop();
                Directory.Delete(root, true);
            }
        }

        private static Socket CreateBoundLoopbackListener()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            return listener;
        }

        private static Socket ConnectTo(Socket listener)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(listener.LocalEndPoint);
            return socket;
        }
    }
}
