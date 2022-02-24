using System.Collections.Generic;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class SendMainInventoryMoveItemProtocol
    {
        private const short ProtocolId = 6;
        private readonly ISocket _socket;

        public SendMainInventoryMoveItemProtocol(ISocket socket)
        {
            _socket = socket;
        }

        public void Send(int playerId, int fromSlot, int toSlot, int itemCount)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(playerId));
            packet.AddRange(ToByteList.Convert(fromSlot));
            packet.AddRange(ToByteList.Convert(toSlot));
            packet.AddRange(ToByteList.Convert(itemCount));

            _socket.Send(packet.ToArray());
        }
    }
}