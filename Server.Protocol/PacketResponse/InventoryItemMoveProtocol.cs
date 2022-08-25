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
    /// <summary>
    /// インベントリでマウスを使ってアイテムの移動を操作するプロトコルです
    /// </summary>
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
            
            var fromInventory = GetInventory(data.FromInventoryId, data.PlayerId, data.FromInventoryX, data.FromInventoryY);
            if (fromInventory == null)return new List<List<byte>>();
            var toInventory = GetInventory(data.ToInventoryId, data.PlayerId, data.ToInventoryX, data.ToInventoryY);
            if (toInventory == null)return new List<List<byte>>();


            InventoryItemMoveService.Move(
                    _itemStackFactory,fromInventory,data.FromInventorySlot,toInventory,data.ToInventorySlot,data.Count);

            return new List<List<byte>>();
        }

        private IOpenableInventory GetInventory(int inventoryId,int playerId, int x, int y)
        {
            var inventoryType = (ItemMoveInventoryType)Enum.ToObject(typeof(ItemMoveInventoryType), inventoryId);
            IOpenableInventory inventory = null;
            switch (inventoryType)
            {
                case ItemMoveInventoryType.MainInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
                    break;
                case ItemMoveInventoryType.CraftInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).CraftingOpenableInventory;
                    break;
                case ItemMoveInventoryType.GrabInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).GrabInventory;
                    break;
                case ItemMoveInventoryType.BlockInventory:
                    inventory = _openableBlockDatastore.ExistsComponentBlock(x,y) ? _openableBlockDatastore.GetBlock(x, y) : null;; 
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

        public InventoryItemMoveProtocolMessagePack(int playerId,int count, ItemMoveInventoryInfo fromInventory,ItemMoveInventoryInfo toInventory)
        {
            Tag = InventoryItemMoveProtocol.Tag;
            PlayerId = playerId;
            Count = count;

            //メッセージパックでenumは思いらしいのでintを使う
            FromInventoryId = (int)fromInventory.ItemMoveInventoryType;
            FromInventorySlot = fromInventory.Slot;
            FromInventoryX = fromInventory.X;
            FromInventoryY = fromInventory.Y;
            
            ToInventoryId = (int)toInventory.ItemMoveInventoryType;
            ToInventorySlot = toInventory.Slot;
            ToInventoryX = toInventory.X;
            ToInventoryY = toInventory.Y;
        }

        public int PlayerId { get; set; }
        public int Count { get; set; }
        public int FromInventoryId { get; set; }
        public int FromInventorySlot { get; set; }
        public int FromInventoryX { get; set; }
        public int FromInventoryY { get; set; }
        
        public int ToInventoryId { get; set; }
        public int ToInventorySlot { get; set; }
        public int ToInventoryX { get; set; }
        public int ToInventoryY { get; set; }
        
        

    }

    public enum ItemMoveInventoryType
    {
        MainInventory,
        CraftInventory,
        GrabInventory,
        BlockInventory,
    }

    public class ItemMoveInventoryInfo
    {
        public readonly ItemMoveInventoryType ItemMoveInventoryType;
        public readonly int Slot;
        public readonly int X;
        public readonly int Y;

        public ItemMoveInventoryInfo(ItemMoveInventoryType itemMoveInventoryType, int slot, int x, int y)
        {
            ItemMoveInventoryType = itemMoveInventoryType;
            Slot = slot;
            X = x;
            Y = y;
        }
    }
}