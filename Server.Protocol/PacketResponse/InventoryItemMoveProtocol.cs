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
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload.ToArray());
            
            var inventory = GetInventory(data.InventoryId, data.PlayerId, x:data.X, data.Y);
            if (inventory == null)return new List<List<byte>>();
            
            var grabInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).GrabInventory;

            
            if (data.ToGrab)
            {
                new InventoryItemMoveService().Move(
                    _itemStackFactory,inventory,data.Slot,grabInventory,0,data.Count);
            }
            else
            {
                new InventoryItemMoveService().Move(
                    _itemStackFactory,grabInventory,0,inventory,data.Slot,data.Count);
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
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public InventoryItemMoveProtocolMessagePack()
        {
        }

        public InventoryItemMoveProtocolMessagePack(int playerId, bool toGrab, InventoryType inventoryType, int slot, int count, int x, int y)
        {
            Tag = InventoryItemMoveProtocol.Tag;
            PlayerId = playerId;
            ToGrab = toGrab;
            InventoryId = (int)inventoryType;
            Slot = slot;
            Count = count;
            X = x;
            Y = y;
        }

        public bool ToGrab { get; set; }
        public int InventoryId { get; set; }
        public int PlayerId { get; set; }
        public int Slot { get; set; }
        public int Count { get; set; }
        
        public int X { get; set; }
        public int Y { get; set; }
    }

    public enum InventoryType
    {
        MainInventory,
        CraftInventory,
        BlockInventory
    }
}