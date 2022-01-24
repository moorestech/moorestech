using System.Collections.Generic;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Send;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendBlockInventoryPlayerInventoryMoveItemProtocol:
        ISendBlockInventoryPlayerInventoryMoveItemProtocol
    {
        private const short ProtocolId = 5;
        private readonly ISocket _socket;

        public SendBlockInventoryPlayerInventoryMoveItemProtocol(ISocket socket)
        {
            _socket = socket;
        }
        public void Send(bool toBlock, 
            int playerId, int playerInventorySlot,
            Vector2 blockPosition, int blockInventorySlot,
            int itemCount)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(toBlock ? (short)0 : (short)1));
            packet.AddRange(ToByteList.Convert(playerId));
            packet.AddRange(ToByteList.Convert(playerInventorySlot));
            packet.AddRange(ToByteList.Convert(blockPosition.x));
            packet.AddRange(ToByteList.Convert(blockPosition.y));
            packet.AddRange(ToByteList.Convert(blockInventorySlot));
            packet.AddRange(ToByteList.Convert(itemCount));
            
            _socket.Send(packet.ToArray());
        }
    }
}