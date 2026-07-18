using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class EventStreamMessagePackTest
    {
        // envelopeのTag/中身がラウンドトリップ保持
        // Tag and inner event survive a round trip
        [Test]
        public void SerializeDeserializeRoundTrip()
        {
            var payload = new byte[] { 1, 2, 3 };
            var packet = new EventStreamMessagePack(new EventMessagePack("va:event:test", payload));
            var bytes = MessagePackSerializer.Serialize(packet);

            // クライアントのルーティングと同じ手順: base型でTagを読む → envelope型で中身を読む
            // Same as client routing: read Tag via base type, then the full envelope
            var basePacket = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(bytes);
            Assert.AreEqual(EventStreamMessagePack.ProtocolTag, basePacket.Tag);

            var deserialized = MessagePackSerializer.Deserialize<EventStreamMessagePack>(bytes);
            Assert.AreEqual("va:event:test", deserialized.Event.Tag);
            CollectionAssert.AreEqual(payload, deserialized.Event.Payload);
        }
    }
}
