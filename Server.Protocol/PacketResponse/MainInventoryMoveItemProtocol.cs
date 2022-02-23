using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.PacketHandle.PacketResponse;
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
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort(); //パケットID
            var playerId = payloadData.MoveNextToGetInt();
            var fromSlot = payloadData.MoveNextToGetInt();
            var toSlot = payloadData.MoveNextToGetInt();
            var itemCount = payloadData.MoveNextToGetInt();

            var mainInventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;

            new InventoryItemMove().Move(_itemStackFactory, mainInventory, fromSlot, mainInventory, toSlot,
                itemCount);

            return new List<byte[]>();
        }
    }
}