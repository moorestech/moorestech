using Client.Network.API;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Protocol;

namespace Client.Tests.Network
{
    // 先頭だけ読む経路が、全体デシリアライズと同じ振り分け結果になることを固定する
    // Pins that the header-only read routes packets the same way a full deserialize did
    public class ProtocolPacketHeaderTest
    {
        [Test]
        public void イベントpushはイベントストリームと判定される()
        {
            var packet = new EventStreamMessagePack(new EventMessagePack("va:event:test", new byte[] { 1, 2, 3 }));
            var header = ProtocolPacketHeader.Read(MessagePackSerializer.Serialize(packet));

            Assert.IsTrue(header.IsEventStream);
        }

        [Test]
        public void 応答パケットはSequenceIdを読み取りイベント扱いしない()
        {
            var packet = new EventStreamMessagePack(new EventMessagePack("va:event:test", new byte[] { 1 })) { Tag = "va:getMapObjectInfo", SequenceId = 42 };
            var header = ProtocolPacketHeader.Read(MessagePackSerializer.Serialize(packet));

            Assert.IsFalse(header.IsEventStream);
            Assert.AreEqual(42, header.SequenceId);
        }

        [Test]
        public void イベントタグの前方一致だけではイベント扱いしない()
        {
            var packet = new EventStreamMessagePack(new EventMessagePack("va:event:test", new byte[] { 1 })) { Tag = EventStreamMessagePack.ProtocolTag + "X" };
            var header = ProtocolPacketHeader.Read(MessagePackSerializer.Serialize(packet));

            Assert.IsFalse(header.IsEventStream);
        }
    }
}
