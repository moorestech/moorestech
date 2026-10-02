using Core.Update;
using System.Linq;
using Server.Util.MessagePack.BeltTransport;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class TrainFullSnapshotEventPacketTest
    {
        // 確定tick末尾にrail・belt・trainを同じ境界で送る。
        // Send rail, belt and train at the same committed tick-end boundary.
        [Test]
        public void HandshakePushesAllSnapshotsAtCommittedTickEnd()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var sink = new CapturedEventSink();
            var context = new PacketResponseContext(sink);

            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack("steam:1"));
            var response = packetResponse.GetPacketResponse(handshake, context);

            Assert.IsTrue(0 < response.Count);
            Assert.IsEmpty(sink.Events, "Snapshot publication waits for the committed boundary.");
            GameUpdater.UpdateOneTick();
            var initial = sink.Events.Where(packet => packet.Tag == TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag ||
                packet.Tag == TrainFullSnapshotEventPacket.BeltFullSnapshotEventTag || packet.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag).ToArray();
            Assert.AreEqual(3, initial.Length);
            Assert.AreEqual(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, initial[0].Tag);
            Assert.AreEqual(TrainFullSnapshotEventPacket.BeltFullSnapshotEventTag, initial[1].Tag);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, initial[2].Tag);
            var belt = MessagePackSerializer.Deserialize<BeltSnapshotMessagePack>(initial[1].Payload);
            var train = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(initial[2].Payload);
            Assert.AreEqual(GameUpdater.CurrentTick, belt.Tick);
            Assert.AreEqual(serviceProvider.GetRequiredService<TrainUpdateService>().GetCurrentTick(), train.ServerTick);
            Assert.AreEqual(serviceProvider.GetRequiredService<TrainUpdateService>().GetCurrentTickSequenceId(), train.WatermarkTickSequenceId);

        }

        // snapshot pushがtickSequenceIdを新規消費しないことを確認（seq穴の防止）
        // Snapshot push must not consume a new tick sequence id (no gaps for other clients)
        [Test]
        public void SnapshotPushDoesNotConsumeTickSequenceId()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();

            var before = trainUpdateService.GetCurrentTickSequenceId();

            var context = new PacketResponseContext(new CapturedEventSink());
            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack("steam:1"));
            packetResponse.GetPacketResponse(handshake, context);

            Assert.AreEqual(before, trainUpdateService.GetCurrentTickSequenceId());
            var snapshots = serviceProvider.GetRequiredService<TrainFullSnapshotEventPacket>();
            snapshots.SendPendingInitialSnapshots();
            Assert.AreEqual(before, trainUpdateService.GetCurrentTickSequenceId());
        }
    }
}
