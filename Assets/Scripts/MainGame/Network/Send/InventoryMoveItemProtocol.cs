using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
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
        public void Send(int count,ItemMoveType itemMoveType, FromItemMoveInventoryInfo fromInventory,ToItemMoveInventoryInfo toInventory)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(
                _playerId, count,itemMoveType, fromInventory, toInventory)).ToList());
        }
    }
}