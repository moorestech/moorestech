using System;
using Server.Protocol.PacketResponse.Handshake;

namespace Client.Network.API
{
    // サーバーがハンドシェイクを拒否した
    // The server rejected the handshake
    public class PlayerHandshakeRejectedException : Exception
    {
        public readonly HandshakeRejection Rejection;

        public PlayerHandshakeRejectedException(HandshakeRejection rejection) : base($"ハンドシェイクが拒否されました: {rejection}")
        {
            Rejection = rejection;
        }
    }
}
