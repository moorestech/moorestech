using System.Collections.Generic;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendBlockInventoryMainInventoryMoveItemProtocol
    {
        private const short ProtocolId = 5;
        private readonly ISocket _socket;

        public SendBlockInventoryMainInventoryMoveItemProtocol(ISocket socket)
        {
            _socket = socket;
        }
        public void Send(int playerId,bool toBlock, Vector2Int blockPosition,
            int fromSlot,
            int toSlot,
            int itemCount)
        {
            var packet = new List<byte>();

            //プレイヤーインベントリから移動したのか、ブロックインベントリから移動したのかを設定
            var playerInventorySlot = toSlot;
            var blockInventorySlot = fromSlot; 
            if (toBlock)
            {
                playerInventorySlot = fromSlot;
                blockInventorySlot = toSlot;
            }
            
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