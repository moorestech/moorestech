using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryMoveItemProtocol : IPacketResponse
    {
        private readonly IWorldBlockComponentDatastore<IInventory> _inventoryDatastore;
        private readonly ItemStackFactory _itemStackFactory;

        public BlockInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _inventoryDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IInventory>>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort(); //パケットID
            var x = payloadData.MoveNextToGetInt();
            var y = payloadData.MoveNextToGetInt();
            var fromSlot = payloadData.MoveNextToGetInt();
            var toSlot = payloadData.MoveNextToGetInt();
            var itemCount = payloadData.MoveNextToGetInt();

            if (!_inventoryDatastore.ExistsComponentBlock(x, y)) return new List<byte[]>();
            var inventoryBlock = _inventoryDatastore.GetBlock(x, y);
            
            
            new InventoryItemMove().
                Move(_itemStackFactory, inventoryBlock, fromSlot, inventoryBlock, toSlot,
                itemCount);

            return new List<byte[]>();
        }
    }
}