using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;
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
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert(ProtocolId));
            payload.Add(toGrab ? (byte) 0 : (byte) 1);
            payload.Add((byte)inventoryType);
            payload.AddRange(ToByteList.Convert(_playerId));
            payload.AddRange(ToByteList.Convert(inventorySlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            payload.AddRange(ToByteList.Convert(x));
            payload.AddRange(ToByteList.Convert(y));
            
            _socket.Send(payload.ToArray());
        }
    }

    public enum InventoryType
    {
        MainInventory,
        CraftInventory,
        BlockInventory
    }
}