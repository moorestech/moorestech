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
        private readonly ISocketSender _socketSender;
        private const short ProtocolId = 5;
        private readonly int _playerId;

        public InventoryMoveItemProtocol(PlayerConnectionSetting playerConnectionSetting,ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }
        public void Send(bool toGrab, InventoryType inventoryType, int inventorySlot, int itemCount, int x = 0, int y = 0)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(
                _playerId,toGrab,inventoryType,inventorySlot,itemCount,x,y)).ToList());
        }
    }
}