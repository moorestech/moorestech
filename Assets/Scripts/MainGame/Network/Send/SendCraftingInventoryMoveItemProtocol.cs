using System;
using System.Collections.Generic;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class SendCraftingInventoryMoveItemProtocol
    {
        private const short ProtocolId = 13;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftingInventoryMoveItemProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void Send(int fromSlot, int toSlot, int itemCount)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(_playerId));
            packet.AddRange(ToByteList.Convert(fromSlot));
            packet.AddRange(ToByteList.Convert(toSlot));
            packet.AddRange(ToByteList.Convert(itemCount));

            _socket.Send(packet.ToArray());
        }
    }
}