using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using Server.PacketHandle.PacketResponse.Util;
using Server.Util;
using World;

namespace Server.PacketHandle.PacketResponse
{
    public class BlockInventoryPlayerInventoryMoveItemProtocol : IPacketResponse
    {
        private readonly WorldBlockDatastore _worldBlockDatastore;
        private readonly PlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;

        public BlockInventoryPlayerInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<WorldBlockDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            var flag = payloadData.MoveNextToGetShort();
            var playerId = payloadData.MoveNextToGetInt();
            var playerInventorySlot = payloadData.MoveNextToGetInt();
            var blockX = payloadData.MoveNextToGetInt();
            var blockY = payloadData.MoveNextToGetInt();
            var blockInventorySlot = payloadData.MoveNextToGetInt();
            var moveItemAmount = payloadData.MoveNextToGetInt();
            
            var blockInventory = (IInventory)_worldBlockDatastore.GetBlockInventory(blockX, blockY);
            var playerInventory = (IInventory)_playerInventoryDataStore.GetInventoryData(playerId);

            var inventoryItemMove = new InventoryItemMove();
            //フラグが0の時はプレイヤーインベントリからブロックインベントリにアイテムを移す
            if (flag == 0)
            {
                inventoryItemMove.Move(_itemStackFactory,playerInventory,playerInventorySlot,blockInventory,blockInventorySlot,moveItemAmount);
            }
            else if (flag == 1)
            {
                inventoryItemMove.Move(_itemStackFactory,blockInventory,blockInventorySlot,playerInventory,playerInventorySlot,moveItemAmount);
            }

            return new List<byte[]>();
        }
    }
}