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
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// インベントリでマウスを使ってアイテムの移動を操作するプロトコルです
    /// </summary>
    public class InventoryItemMoveProtocol : IPacketResponse
    {
        public const string Tag = "va:invItemMove";
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;

        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload.ToArray());
            
            var fromInventory = GetInventory(data.FromInventory.InventoryType, data.PlayerId, data.FromInventory.X, data.FromInventory.Y);
            if (fromInventory == null)return new List<List<byte>>();
            var toInventory = GetInventory(data.ToInventory.InventoryType, data.PlayerId, data.ToInventory.X, data.ToInventory.Y);
            if (toInventory == null)return new List<List<byte>>();

            var fromSlot = data.FromInventory.Slot;

            switch (data.ItemMoveType)
            {
                case ItemMoveType.SwapSlot:
                    var toSlot = data.ToInventory.Slot;
                    InventoryItemMoveService.Move(
                        _itemStackFactory,fromInventory,fromSlot,toInventory,toSlot,data.Count);
                    break;
                case ItemMoveType.InsertSlot:
                {
                    InventoryItemInsertService.Insert(fromInventory,fromSlot,toInventory,data.Count);
                    break;
                }
            }

            return new List<List<byte>>();
        }

        private IOpenableInventory GetInventory(ItemMoveInventoryType inventoryType,int playerId, int x, int y)
        {
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
                    inventory = _worldBlockDatastore.ExistsComponentBlock<IOpenableInventory>(x,y) ? _worldBlockDatastore.GetBlock<IOpenableInventory>(x, y) : null;; 
                    break;
            }
            return inventory;
        }
        
        
    }

    
    [MessagePackObject(keyAsPropertyName :true)]
    public class InventoryItemMoveProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public InventoryItemMoveProtocolMessagePack() { }

        public InventoryItemMoveProtocolMessagePack(int playerId,int count,ItemMoveType itemMoveType, FromItemMoveInventoryInfo fromInventory,ToItemMoveInventoryInfo toInventory)
        {
            Tag = InventoryItemMoveProtocol.Tag;
            PlayerId = playerId;
            Count = count;
            
            ItemMoveTypeId = (int)itemMoveType;
            FromInventory = new ItemMoveInventoryInfoMessagePack(fromInventory);
            ToInventory = new ItemMoveInventoryInfoMessagePack(toInventory);
        }

        public int PlayerId { get; set; }
        public int Count { get; set; }
        public int ItemMoveTypeId { get; set; }
        public ItemMoveType ItemMoveType => (ItemMoveType)ItemMoveTypeId;
        
        public ItemMoveInventoryInfoMessagePack FromInventory { get; set; }
        public ItemMoveInventoryInfoMessagePack ToInventory { get; set; }

    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class ItemMoveInventoryInfoMessagePack
    {
        [Obsolete("シリアライズ用の値です。InventoryTypeを使用してください。")]
        public int InventoryId { get; set; }
        public ItemMoveInventoryType InventoryType => (ItemMoveInventoryType)Enum.ToObject(typeof(ItemMoveInventoryType), InventoryId);
        
        
        public  int Slot{ get; set; }
        public  int X { get; set; }
        public  int Y { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public  ItemMoveInventoryInfoMessagePack(){}
        public ItemMoveInventoryInfoMessagePack(FromItemMoveInventoryInfo info)
        {
            //メッセージパックでenumは重いらしいのでintを使う
            InventoryId = (int)info.ItemMoveInventoryType;
            Slot = info.Slot;
            X = info.X;
            Y = info.Y;
        }
        
        public ItemMoveInventoryInfoMessagePack(ToItemMoveInventoryInfo info)
        {
            //メッセージパックでenumは重いらしいのでintを使う
            InventoryId = (int)info.ItemMoveInventoryType;
            Slot = info.Slot;
            X = info.X;
            Y = info.Y;
        }
    }
}