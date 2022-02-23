using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class MainInventoryMoveItemProtocol : IPacketResponse
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;

        public MainInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort(); //パケットID
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var fromSlot = byteListEnumerator.MoveNextToGetInt();
            var toSlot = byteListEnumerator.MoveNextToGetInt();
            var itemCount = byteListEnumerator.MoveNextToGetInt();

            var mainInventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;

            new InventoryItemMove().Move(_itemStackFactory, mainInventory, fromSlot, mainInventory, toSlot,
                itemCount);

            return new List<byte[]>();
        }
    }
}