using System.Collections.Generic;
using Core.Item;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using Server.PacketHandle.PacketResponse.Util;
using Server.Util;

namespace Server.PacketHandle.PacketResponse
{
    public class PlayerInventoryMoveItemProtocol : IPacketResponse
    {
        private readonly PlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;
        public PlayerInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort(); //パケットID
            var playerId = payloadData.MoveNextToGetInt();
            var fromSlot = payloadData.MoveNextToGetInt();
            var toSlot = payloadData.MoveNextToGetInt();
            var itemCount = payloadData.MoveNextToGetInt();
            
            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId);

            new InventoryItemMove().Move(_itemStackFactory,playerInventory,fromSlot,playerInventory,toSlot,itemCount);

            return new List<byte[]>();
        }
    }
}