using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class RequestBlockInventoryProtocol
    {
        private const short ProtocolId = 9;
        private readonly ISocket _socket;

        public RequestBlockInventoryProtocol(ISocket socket)
        {
            _socket = socket;
        }

        public void Send(int x, int y)
        {
            _socket.Send(MessagePackSerializer.Serialize(new RequestBlockInventoryRequestProtocolMessagePack(
                x,y)).ToList());
        }
    }
}