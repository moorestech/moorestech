using System;
using System.Collections.Generic;
using Core.Inventory;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     インベントリでマウスを使ってアイテムの移動を操作するプロトコルです
    /// </summary>
    public class InventoryItemMoveProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invItemMove";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload.ToArray());

            var fromInventory = GetInventory(data.FromInventory.InventoryType, data.PlayerId, data.FromInventory.InventoryIdentifier);
            if (fromInventory == null) return null;

            var fromSlot = data.FromInventory.Slot;
            if (data.FromInventory.InventoryType == ItemMoveInventoryType.SubInventory)
                fromSlot -= PlayerInventoryConst.MainInventorySize;


            var toInventory = GetInventory(data.ToInventory.InventoryType, data.PlayerId, data.ToInventory.InventoryIdentifier);
            if (toInventory == null) return null;

            var toSlot = data.ToInventory.Slot;
            if (data.ToInventory.InventoryType == ItemMoveInventoryType.SubInventory)
                toSlot -= PlayerInventoryConst.MainInventorySize;


            switch (data.ItemMoveType)
            {
                case ItemMoveType.SwapSlot:
                    InventoryItemMoveService.Move(fromInventory, fromSlot, toInventory, toSlot, data.Count);
                    break;
                case ItemMoveType.InsertSlot:
                    InventoryItemInsertService.Insert(fromInventory, fromSlot, toInventory, data.Count);
                    break;
            }

            return null;
        }

        private IOpenableInventory GetInventory(ItemMoveInventoryType inventoryType, int playerId, InventoryIdentifierMessagePack inventoryIdentifier)
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
                case ItemMoveInventoryType.SubInventory:
                case ItemMoveInventoryType.TrainInventory:
                    // ブロック/列車インベントリの場合はInventoryIdentifierから情報を取得
                    // Get information from InventoryIdentifier for block/train inventory
                    if (inventoryIdentifier == null) return null;

                    // InventoryIdentifierのタイプに応じて処理を分岐
                    // Branch processing according to InventoryIdentifier type
                    switch (inventoryIdentifier.InventoryType)
                    {
                        case Server.Util.MessagePack.InventoryType.Block:
                            var pos = inventoryIdentifier.BlockPosition.Vector3Int;
                            inventory = ServerContext.WorldBlockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(pos)
                                ? ServerContext.WorldBlockDatastore.GetBlock<IOpenableBlockInventoryComponent>(pos)
                                : null;
                            break;
                        case Server.Util.MessagePack.InventoryType.Train:
                            // TODO: 列車インベントリの取得処理を実装
                            // TODO: Implement train inventory retrieval
                            // 現時点では列車インベントリシステムが完全には実装されていないため、nullを返す
                            // Currently returns null as train inventory system is not fully implemented
                            inventory = null;
                            break;
                    }
                    break;
            }

            return inventory;
        }
        
        [MessagePackObject]
        public class InventoryItemMoveProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int Count { get; set; }
            [Key(4)] public int ItemMoveTypeId { get; set; }
            [IgnoreMember] public ItemMoveType ItemMoveType => (ItemMoveType)ItemMoveTypeId;
            [Key(5)] public ItemMoveInventoryInfoMessagePack FromInventory { get; set; }
            [Key(6)] public ItemMoveInventoryInfoMessagePack ToInventory { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public InventoryItemMoveProtocolMessagePack() { }
            public InventoryItemMoveProtocolMessagePack(int playerId, int count, ItemMoveType itemMoveType,
                ItemMoveInventoryInfo inventory, int fromSlot,
                ItemMoveInventoryInfo toInventory, int toSlot)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Count = count;
                
                ItemMoveTypeId = (int)itemMoveType;
                FromInventory = new ItemMoveInventoryInfoMessagePack(inventory, fromSlot);
                ToInventory = new ItemMoveInventoryInfoMessagePack(toInventory, toSlot);
            }
        }
        
        [MessagePackObject]
        public class ItemMoveInventoryInfoMessagePack
        {
            [Obsolete("シリアライズ用の値です。InventoryTypeを使用してください。")]
            [Key(2)] public int InventoryId { get; set; }

            [IgnoreMember] public ItemMoveInventoryType InventoryType => (ItemMoveInventoryType)Enum.ToObject(typeof(ItemMoveInventoryType), InventoryId);

            [Key(3)] public int Slot { get; set; }

            /// <summary>
            /// ブロックまたは列車インベントリの識別子
            /// Identifier for block or train inventory
            /// </summary>
            [Key(4)] public InventoryIdentifierMessagePack InventoryIdentifier { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ItemMoveInventoryInfoMessagePack() { }
            public ItemMoveInventoryInfoMessagePack(ItemMoveInventoryInfo info, int slot)
            {
                // メッセージパックでenumは重いらしいのでintを使う
                // MessagePack enum is heavy, so use int
                InventoryId = (int)info.ItemMoveInventoryType;
                Slot = slot;
                InventoryIdentifier = info.SubInventoryIdentifier;
            }
        }
    }
}