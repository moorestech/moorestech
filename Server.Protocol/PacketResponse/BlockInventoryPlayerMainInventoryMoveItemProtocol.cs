using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.PacketHandle.PacketResponse;
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
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            var flag = payloadData.MoveNextToGetShort();
            var playerId = payloadData.MoveNextToGetInt();
            var playerMainInventorySlot = payloadData.MoveNextToGetInt();
            var blockX = payloadData.MoveNextToGetInt();
            var blockY = payloadData.MoveNextToGetInt();
            var blockInventorySlot = payloadData.MoveNextToGetInt();
            var moveItemCount = payloadData.MoveNextToGetInt();

            var blockInventory = (IInventory) _worldBlockDatastore.GetBlock(blockX, blockY);
            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(playerId).MainInventory;

            var inventoryItemMove = new InventoryItemMove();
            //フラグが0の時はプレイヤーインベントリからブロックインベントリにアイテムを移す
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