using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class InventoryMoveItemProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 5;
        private readonly int _playerId;

        public InventoryMoveItemProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }
        public void Send(bool toGrab, InventoryType inventoryType, int inventorySlot, int itemCount, int x = 0, int y = 0)
        {
            _socket.Send(MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(
                _playerId,toGrab,inventoryType,inventorySlot,itemCount,x,y)).ToList());
        }
    }
}