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
    public class CraftInventoryPlayerMainInventoryMoveItemProtocol : IPacketResponse
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;

        public CraftInventoryPlayerMainInventoryMoveItemProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            var flag = payloadData.MoveNextToGetShort();
            var playerId = payloadData.MoveNextToGetInt();
            var mainInventorySlot = payloadData.MoveNextToGetInt();
            var craftInventorySlot = payloadData.MoveNextToGetInt();
            var moveItemCount = payloadData.MoveNextToGetInt();

            var craftInventory = _playerInventoryDataStore.GetInventoryData(playerId).CraftingInventory;
            var mainInventory = _playerInventoryDataStore.GetInventoryData(playerId).MainInventory;

            var inventoryItemMove = new InventoryItemMove();
            
            //フラグが0の時はメインインベントリからクラフトインベントリにアイテムを移す
            if (flag == 0)
            {
                inventoryItemMove.Move(_itemStackFactory, mainInventory, mainInventorySlot, craftInventory,
                    craftInventorySlot, moveItemCount);
            }
            else if (flag == 1)
            {
                inventoryItemMove.Move(_itemStackFactory, craftInventory, craftInventorySlot, mainInventory,
                    mainInventorySlot, moveItemCount);
            }

            return new List<byte[]>();
        }
    }
}