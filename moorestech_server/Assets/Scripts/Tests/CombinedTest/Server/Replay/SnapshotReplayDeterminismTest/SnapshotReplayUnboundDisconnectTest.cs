using System;
using System.IO;
using Core.Update;
using Game.Context;
using Game.Paths;
using Game.PlayerConnection;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Replay;
using Server.Event;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.Replay.SnapshotReplayDeterminismTest
{
    public class SnapshotReplayUnboundDisconnectTest
    {
        [Test]
        public void ハンドシェイク直後の切断でも採番と接続集合が再生と一致する()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-replay-handshake-close-{Guid.NewGuid():N}");
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
                File.WriteAllText(savePath, provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                var connections = (PlayerConnectionRegistry)provider.GetRequiredService<IPlayerConnectionChecker>();
                var events = provider.GetRequiredService<EventProtocolProvider>();
                var firstContext = new PacketResponseContext(null);
                var firstReceiver = socket.CreateReceiver(creator, firstContext, queue, log);
                log.Start(Path.Combine(root, "packets"), 1);

                // 受信スレッドがハンドシェイクを積んで直ちに切れても、FIFO内では受理後に解除する
                // Even if receive closes immediately after enqueue, the FIFO accepts then disconnects
                firstReceiver.EnqueuePacket(Handshake("steam:1"));
                ConnectionDisconnectEntry.Schedule(firstContext, firstReceiver, queue, connections, events, log);
                GameUpdater.Update();
                Assert.AreEqual(1, firstContext.PlayerId);
                Assert.IsFalse(connections.IsConnected(1));

                // 後続の未知身元は欠番を作らず次のIDを受け取る
                // The next unknown identity receives the next ID without a replay-only gap
                var secondContext = new PacketResponseContext(null);
                socket.CreateReceiver(creator, secondContext, queue, log).EnqueuePacket(Handshake("steam:2"));
                GameUpdater.Update();
                Assert.AreEqual(2, secondContext.PlayerId);
                Assert.IsTrue(connections.IsConnected(2));
                log.Stop();

                var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
                CollectionAssert.AreEqual(new int?[] { null, 1, null }, records.ConvertAll(record => record.PlayerId));
                CollectionAssert.AreEqual(new[] { ReceivedPacketRecordKind.Packet, ReceivedPacketRecordKind.Disconnect,
                    ReceivedPacketRecordKind.Packet }, records.ConvertAll(record => record.Kind));
                var expected = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, savePath, log.SegmentFilePaths(), 2));
                var expectedPlayers = JObject.Parse(expected)["players"];
                var replayedPlayers = JObject.Parse(result.SnapshotJson)["players"];
                Assert.IsTrue(JToken.DeepEquals(expectedPlayers, replayedPlayers));
                Assert.AreEqual(3, (int)replayedPlayers["nextPlayerId"]);
                var replayConnections = ServerContext.GetService<IPlayerConnectionChecker>();
                Assert.IsFalse(replayConnections.IsConnected(1));
                Assert.IsTrue(replayConnections.IsConnected(2));
            }
            finally
            {
                log.Stop();
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void 未紐づけ切断はnullで記録し後続の採番を変えない()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-replay-unbound-close-{Guid.NewGuid():N}");
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
                File.WriteAllText(savePath, provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                var connections = (PlayerConnectionRegistry)provider.GetRequiredService<IPlayerConnectionChecker>();
                var events = provider.GetRequiredService<EventProtocolProvider>();
                var unbound = new PacketResponseContext(null);
                var receiver = socket.CreateReceiver(creator, unbound, queue, log);
                log.Start(Path.Combine(root, "packets"), 1);
                ConnectionDisconnectEntry.Schedule(unbound, receiver, queue, connections, events, log);
                GameUpdater.Update();

                var next = new PacketResponseContext(null);
                socket.CreateReceiver(creator, next, queue, log).EnqueuePacket(Handshake("steam:1"));
                GameUpdater.Update();
                Assert.AreEqual(1, next.PlayerId);
                log.Stop();
                var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
                Assert.AreEqual(ReceivedPacketRecordKind.Disconnect, records[0].Kind);
                Assert.IsNull(records[0].PlayerId);
                var expected = JObject.Parse(provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson())["players"];

                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, savePath, log.SegmentFilePaths(), 2));
                var replayed = JObject.Parse(result.SnapshotJson)["players"];
                Assert.IsTrue(JToken.DeepEquals(expected, replayed));
                Assert.IsTrue(ServerContext.GetService<IPlayerConnectionChecker>().IsConnected(1));
            }
            finally
            {
                log.Stop();
                Directory.Delete(root, true);
            }
        }

        private static byte[] Handshake(string identity)
        {
            return MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(identity));
        }
    }
}
