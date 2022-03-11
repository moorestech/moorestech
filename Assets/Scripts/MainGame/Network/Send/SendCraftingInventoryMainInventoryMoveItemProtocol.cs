using System;
using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class SendCraftingInventoryMainInventoryMoveItemProtocol
    {
        private const short ProtocolId = 12;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftingInventoryMainInventoryMoveItemProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void Send(bool toCrafting, int fromSlot, int toSlot, int itemCount)
        {
            var toCraftingByte = toCrafting ? (short)0 : (short)1;
            var mainSlot = toCrafting ? fromSlot : toSlot;
            var craftingSlot = toCrafting ? toSlot : fromSlot;
            
            
            var packet = new List<byte>();

            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(toCraftingByte));
            packet.AddRange(ToByteList.Convert(_playerId));
            packet.AddRange(ToByteList.Convert(mainSlot));
            packet.AddRange(ToByteList.Convert(craftingSlot));
            packet.AddRange(ToByteList.Convert(itemCount));

            _socket.Send(packet.ToArray());
        }
    }
}