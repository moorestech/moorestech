using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryPlayerMainInventoryMoveItemProtocol : IPacketResponse
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;

        public BlockInventoryPlayerMainInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var flag = byteListEnumerator.MoveNextToGetShort();
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var playerMainInventorySlot = byteListEnumerator.MoveNextToGetInt();
            var blockX = byteListEnumerator.MoveNextToGetInt();
            var blockY = byteListEnumerator.MoveNextToGetInt();
            var blockInventorySlot = byteListEnumerator.MoveNextToGetInt();
            var moveItemCount = byteListEnumerator.MoveNextToGetInt();
            Console.WriteLine("BlockInventoryPlayerMainInventoryMoveItemProtocol " + blockInventorySlot);

            var blockInventory = (IOpenableInventory) _worldBlockDatastore.GetBlock(blockX, blockY);
            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;

            var inventoryItemMove = new InventoryItemMove();
            //フラグが0の時はメインインベントリからブロックインベントリにアイテムを移す
            if (flag == 0)
            {
                inventoryItemMove.Move(_itemStackFactory, playerMainInventory, playerMainInventorySlot, blockInventory,
                    blockInventorySlot, moveItemCount);
            }
            else if (flag == 1)
            {
                inventoryItemMove.Move(_itemStackFactory, blockInventory, blockInventorySlot, playerMainInventory,
                    playerMainInventorySlot, moveItemCount);
            }

            return new List<byte[]>();
        }
    }
}