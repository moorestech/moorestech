using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainResyncProtocolTest
    {
        // resync要求でrail+trainのfull snapshotがイベント経路にpushされることを確認
        // Resync request pushes rail+train full snapshots over the event stream
        [Test]
        public void ResyncWithRailGraphPushesBothSnapshots()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var context = new PacketResponseContext();
            var sink = new CapturedEventSink();
            context.SetEventSink(sink);

            // handshakeでsink登録と初期push（ここで捕捉分はクリアする）
            // Handshake registers the sink and pushes initial snapshots; clear those captures
            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            packetResponse.GetPacketResponse(handshake, context);
            sink.TakeAll();

            var resync = MessagePackSerializer.Serialize(new TrainResyncProtocol.RequestMessagePack(true));
            var response = packetResponse.GetPacketResponse(resync, context);

            Assert.IsTrue(0 < response.Count);
            var events = sink.TakeAll();
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, events[0].Tag);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, events[1].Tag);
        }

        // IncludeRailGraph=falseでtrainのみpush
        // IncludeRailGraph=false pushes the train snapshot only
        [Test]
        public void ResyncTrainOnlyPushesTrainSnapshot()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var context = new PacketResponseContext();
            var sink = new CapturedEventSink();
            context.SetEventSink(sink);

            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            packetResponse.GetPacketResponse(handshake, context);
            sink.TakeAll();

            var resync = MessagePackSerializer.Serialize(new TrainResyncProtocol.RequestMessagePack(false));
            packetResponse.GetPacketResponse(resync, context);

            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, events[0].Tag);
        }
    }
}
