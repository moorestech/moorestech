using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using PlayerInventory;
using Server.PacketHandle.PacketResponse.Util;
using Server.Util;
using World;

namespace Server.PacketHandle.PacketResponse
{
    public class BlockInventoryPlayerInventoryMoveItemProtocol : IPacketResponse
    {
        private WorldBlockDatastore _worldBlockDatastore;
        private PlayerInventoryDataStore _playerInventoryDataStore;

        public BlockInventoryPlayerInventoryMoveItemProtocol(WorldBlockDatastore worldBlockDatastore, PlayerInventoryDataStore playerInventoryDataStore)
        {
            _worldBlockDatastore = worldBlockDatastore;
            _playerInventoryDataStore = playerInventoryDataStore;
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
                inventoryItemMove.Move(playerInventory,playerInventorySlot,blockInventory,blockInventorySlot,moveItemAmount);
            }
            else if (flag == 1)
            {
                inventoryItemMove.Move(blockInventory,blockInventorySlot,playerInventory,playerInventorySlot,moveItemAmount);
            }

            return new List<byte[]>();
        }
    }
}