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
        // handshake処理中にrail→trainの順でfull snapshotがpushされることを確認
        // Handshake pushes rail then train full snapshots, in that order, before the response returns
        [Test]
        public void HandshakePushesRailThenTrainSnapshotBeforeResponse()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var context = new PacketResponseContext();
            var sink = new CapturedEventSink();
            context.SetEventSink(sink);

            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            var response = packetResponse.GetPacketResponse(handshake, context);

            // GetPacketResponseが返った時点でsinkに両snapshotが積まれている＝応答より先にワイヤへ載る
            // Both snapshots are already in the sink when the response returns, so they precede it on the wire
            Assert.IsTrue(0 < response.Count);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, sink.Events[0].Tag);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, sink.Events[1].Tag);
        }

        // snapshot pushがtickSequenceIdを新規消費しないことを確認（seq穴の防止）
        // Snapshot push must not consume a new tick sequence id (no gaps for other clients)
        [Test]
        public void SnapshotPushDoesNotConsumeTickSequenceId()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();

            var before = trainUpdateService.GetCurrentTickSequenceId();

            var context = new PacketResponseContext();
            context.SetEventSink(new CapturedEventSink());
            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            packetResponse.GetPacketResponse(handshake, context);

            Assert.AreEqual(before, trainUpdateService.GetCurrentTickSequenceId());
        }
    }
}
