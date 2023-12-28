using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     インベントリでマウスを使ってアイテムの移動を操作するプロトコルです
    /// </summary>
    public class InventoryItemMoveProtocol : IPacketResponse
    {
        public const string Tag = "va:invItemMove";
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        private readonly IWorldBlockDatastore _worldBlockDatastore;

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
            if (fromInventory == null) return new List<List<byte>>();
            
            var fromSlot = data.FromInventory.Slot;
            if (data.FromInventory.InventoryType == ItemMoveInventoryType.BlockInventory)
                fromSlot -= PlayerInventoryConst.MainInventorySize;
            
            
            var toInventory = GetInventory(data.ToInventory.InventoryType, data.PlayerId, data.ToInventory.X, data.ToInventory.Y);
            if (toInventory == null) return new List<List<byte>>();
            
            var toSlot = data.ToInventory.Slot;
            if (data.ToInventory.InventoryType == ItemMoveInventoryType.BlockInventory)
                toSlot -= PlayerInventoryConst.MainInventorySize;

            
            switch (data.ItemMoveType)
            {
                case ItemMoveType.SwapSlot:
                    Debug.Log($"Swap from:{data.FromInventory.InventoryType} fromSlot:{fromSlot} to:{data.ToInventory.InventoryType} to:{toSlot} count:{data.Count}");
                    InventoryItemMoveService.Move(_itemStackFactory, fromInventory, fromSlot, toInventory, toSlot, data.Count);
                    break;
                case ItemMoveType.InsertSlot:
                    Debug.Log($"Insert from:{data.FromInventory.InventoryType} fromSlot:{fromSlot} to:{data.ToInventory.InventoryType} count:{data.Count}");
                    InventoryItemInsertService.Insert(fromInventory, fromSlot, toInventory, data.Count);
                    break;
            }

            return new List<List<byte>>();
        }

        private IOpenableInventory GetInventory(ItemMoveInventoryType inventoryType, int playerId, int x, int y)
        {
            IOpenableInventory inventory = null;
            switch (inventoryType)
            {
                case ItemMoveInventoryType.MainInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
                    break;
                case ItemMoveInventoryType.GrabInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).GrabInventory;
                    break;
                case ItemMoveInventoryType.BlockInventory:
                    inventory = _worldBlockDatastore.ExistsComponentBlock<IOpenableInventory>(x, y)
                        ? _worldBlockDatastore.GetBlock<IOpenableInventory>(x, y)
                        : null;
                    break;
            }

            return inventory;
        }
    }


    [MessagePackObject(true)]
    public class InventoryItemMoveProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public InventoryItemMoveProtocolMessagePack()
        {
        }

        public InventoryItemMoveProtocolMessagePack(int playerId, int count, ItemMoveType itemMoveType, 
            ItemMoveInventoryInfo inventory,int fromSlot, 
            ItemMoveInventoryInfo toInventory,int toSlot)
        {
            Tag = InventoryItemMoveProtocol.Tag;
            PlayerId = playerId;
            Count = count;

            ItemMoveTypeId = (int)itemMoveType;
            FromInventory = new ItemMoveInventoryInfoMessagePack(inventory,fromSlot);
            ToInventory = new ItemMoveInventoryInfoMessagePack(toInventory,toSlot);
        }

        public int PlayerId { get; set; }
        public int Count { get; set; }
        public int ItemMoveTypeId { get; set; }
        public ItemMoveType ItemMoveType => (ItemMoveType)ItemMoveTypeId;

        public ItemMoveInventoryInfoMessagePack FromInventory { get; set; }
        public ItemMoveInventoryInfoMessagePack ToInventory { get; set; }
    }

    [MessagePackObject(true)]
    public class ItemMoveInventoryInfoMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ItemMoveInventoryInfoMessagePack()
        {
        }

        public ItemMoveInventoryInfoMessagePack(ItemMoveInventoryInfo info,int slot)
        {
            //メッセージパックでenumは重いらしいのでintを使う
            InventoryId = (int)info.ItemMoveInventoryType;
            Slot = slot;
            X = info.X;
            Y = info.Y;
        }

        [Obsolete("シリアライズ用の値です。InventoryTypeを使用してください。")]
        public int InventoryId { get; set; }

        public ItemMoveInventoryType InventoryType =>
            (ItemMoveInventoryType)Enum.ToObject(typeof(ItemMoveInventoryType), InventoryId);


        public int Slot { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}