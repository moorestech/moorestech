using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class InventoryMoveItemProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 16;
        private readonly int _playerId;

        public InventoryMoveItemProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y,bool isOpen)
        {
            
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(_playerId));
            packet.Add(isOpen ? (byte)1 : (byte)0);

            _socket.Send(packet.ToArray());
        }
        private List<byte> Send(bool toGrab,InventoryType inventoryType,int inventorySlot,int itemCount,int x = 0,int y = 0)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 5));
            payload.Add(toGrab ? (byte) 0 : (byte) 1);
            payload.Add((byte)inventoryType);
            payload.AddRange(ToByteList.Convert(_playerId));
            payload.AddRange(ToByteList.Convert(inventorySlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            payload.AddRange(ToByteList.Convert(x));
            payload.AddRange(ToByteList.Convert(y));

            return payload;
        }
    }

    public enum InventoryType
    {
        MainInventory,
        CraftInventory,
        BlockInventory
    }
}