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
        private readonly int _playerId;

        public InventoryMoveItemProtocol(PlayerConnectionSetting playerConnectionSetting,ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }
        public void Send(int count, ItemMoveInventoryInfo fromInventory,ItemMoveInventoryInfo toInventory)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(
                _playerId, count, fromInventory, toInventory)).ToList());
        }
    }
}