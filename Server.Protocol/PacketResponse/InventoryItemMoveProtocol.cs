using System;
using System.Collections.Generic;
using System.Text;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class InventoryItemMoveProtocol : IPacketResponse
    {
        public const string Tag = "va:invItemMove";
        
        private readonly IWorldBlockComponentDatastore<IOpenableInventory> _openableBlockDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;

        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _openableBlockDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IOpenableInventory>>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();//packet id
            var toGrab = byteListEnumerator.MoveNextToGetByte() == 0;
            var inventoryId = byteListEnumerator.MoveNextToGetByte();
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var slot = byteListEnumerator.MoveNextToGetInt();
            var moveItemCount = byteListEnumerator.MoveNextToGetInt();
            var x = byteListEnumerator.MoveNextToGetInt();
            var y = byteListEnumerator.MoveNextToGetInt();

            var inventory = GetInventory(inventoryId, playerId, x, y);
            if (inventory == null)return new List<List<byte>>();
            
            var grabInventory = _playerInventoryDataStore.GetInventoryData(playerId).GrabInventory;

            
            if (toGrab)
            {
                new InventoryItemMoveService().Move(
                    _itemStackFactory,inventory,slot,grabInventory,0,moveItemCount);
            }
            else
            {
                new InventoryItemMoveService().Move(
                    _itemStackFactory,grabInventory,0,inventory,slot,moveItemCount);
            }
            

            return new List<List<byte>>();
        }

        private IOpenableInventory GetInventory(int inventoryId,int playerId, int x, int y)
        {
            var inventoryType = (InventoryType)Enum.ToObject(typeof(InventoryType), inventoryId);
            IOpenableInventory inventory = null;
            switch (inventoryType)
            {
                case InventoryType.MainInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
                    break;
                case InventoryType.CraftInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).CraftingOpenableInventory;
                    break;
                case InventoryType.BlockInventory:
                    if (_openableBlockDatastore.ExistsComponentBlock(x,y))
                    {
                        inventory = _openableBlockDatastore.GetBlock(x, y);
                    }
                    break;
            }
            return inventory;
        }
        
        
    }

    
    [MessagePackObject(keyAsPropertyName :true)]
    public class InventoryItemMoveProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
    enum InventoryType
    {
        MainInventory,
        CraftInventory,
        BlockInventory
    }
}