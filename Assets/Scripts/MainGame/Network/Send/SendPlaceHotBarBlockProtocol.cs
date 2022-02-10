using System.Collections.Generic;
using MainGame.Network.Interface;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class SendPlaceHotBarBlockProtocol
    {
        private const short ProtocolId = 8;
        private readonly ISocket _socket;

        public SendPlaceHotBarBlockProtocol(ISocket socket)
        {
            _socket = socket;
        }

        public void Send(int x, int y, short hotBarSlot, int playerId)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(hotBarSlot));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(playerId));

            _socket.Send(packet.ToArray());
        }
    }
}