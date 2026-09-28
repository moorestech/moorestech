using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;

namespace Tests.Util.PlayerIdentity
{
    // 実ハンドシェイクと直接バインドの二通りでテスト用接続を作る
    // Build test connections through a real handshake or direct binding
    public static class BoundPacketContext
    {
        public static PacketResponseContext Handshake(PacketResponseCreator creator, string identity, out int playerId)
        {
            var context = new PacketResponseContext(null);
            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(identity));
            var response = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(creator.GetPacketResponse(payload, context)[0]);
            Assert.AreEqual(HandshakeRejection.None, response.Rejection, $"テスト用ハンドシェイクが拒否された: {response.Rejection}");
            playerId = response.Accepted.PlayerId;
            return context;
        }

        public static PacketResponseContext Bind(int playerId)
        {
            var context = new PacketResponseContext(null);
            Assert.IsTrue(context.TryBindPlayerId(playerId));
            return context;
        }
    }
}
