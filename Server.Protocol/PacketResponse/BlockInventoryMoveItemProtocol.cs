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
        private readonly IWorldBlockComponentDatastore<IOpenableInventory> _inventoryDatastore;
        private readonly ItemStackFactory _itemStackFactory;

        public BlockInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _inventoryDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IOpenableInventory>>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort(); //パケットID
            var x = byteListEnumerator.MoveNextToGetInt();
            var y = byteListEnumerator.MoveNextToGetInt();
            var fromSlot = byteListEnumerator.MoveNextToGetInt();
            var toSlot = byteListEnumerator.MoveNextToGetInt();
            var itemCount = byteListEnumerator.MoveNextToGetInt();

            if (!_inventoryDatastore.ExistsComponentBlock(x, y)) return new List<byte[]>();
            var inventoryBlock = _inventoryDatastore.GetBlock(x, y);
            
            
            new InventoryItemMove().
                Move(_itemStackFactory, inventoryBlock, fromSlot, inventoryBlock, toSlot,
                itemCount);

            return new List<byte[]>();
        }
    }
}