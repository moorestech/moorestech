using System.Collections.Generic;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Send;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendBlockInventoryMoveItemProtocol : ISendBlockInventoryMoveItemProtocol
    {
        private const short ProtocolId = 7;
        private readonly ISocket _socket;

        public SendBlockInventoryMoveItemProtocol(ISocket socket)
        {
            _socket = socket;
        }

        public void Send(Vector2Int position, int fromSlot, int toSlot, int itemCount)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(position.x));
            packet.AddRange(ToByteList.Convert(position.y));
            packet.AddRange(ToByteList.Convert(fromSlot));
            packet.AddRange(ToByteList.Convert(toSlot));
            packet.AddRange(ToByteList.Convert(itemCount));
            
            _socket.Send(packet.ToArray());
            
        }
    }
}