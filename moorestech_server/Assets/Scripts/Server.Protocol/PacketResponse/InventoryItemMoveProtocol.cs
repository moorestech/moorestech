using System;
using Core.Inventory;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     インベントリでマウスを使ってアイテムの移動を操作するプロトコルです
    /// </summary>
    public class InventoryItemMoveProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invItemMove";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload);

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
            return inventoryType switch
            {
                ItemMoveInventoryType.MainInventory => _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory,
                ItemMoveInventoryType.GrabInventory => _playerInventoryDataStore.GetInventoryData(playerId).GrabInventory,
                ItemMoveInventoryType.SubInventory => ResolveSubInventory(inventoryIdentifier),
                _ => null,
            };

            #region Internal

            IOpenableInventory ResolveSubInventory(InventoryIdentifierMessagePack identifier)
            {
                // ブロック/列車インベントリの場合はInventoryIdentifierから情報を取得
                // Get information from InventoryIdentifier for block/train inventory.
                if (identifier == null) return null;

                return identifier.InventoryType switch
                {
                    InventoryType.Block => ResolveBlockInventory(identifier),
                    InventoryType.Train => ResolveTrainInventory(identifier),
                    _ => null,
                };
            }

            IOpenableInventory ResolveBlockInventory(InventoryIdentifierMessagePack identifier)
            {
                var pos = identifier.BlockPosition.Vector3Int;
                return ServerContext.WorldBlockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(pos)
                    ? ServerContext.WorldBlockDatastore.GetBlock<IOpenableBlockInventoryComponent>(pos)
                    : null;
            }

            IOpenableInventory ResolveTrainInventory(InventoryIdentifierMessagePack identifier)
            {
                // 列車カーのアイテムコンテナをIOpenableInventoryとして返す
                // Return the target train car item container as IOpenableInventory.
                var trainCarInstanceId = new TrainCarInstanceId(long.Parse(identifier.TrainCarInstanceId));
                if (!_trainUnitLookupDatastore.TryGetTrainCar(trainCarInstanceId, out var trainCar)) return null;
                if (trainCar.Container is not ItemTrainCarContainer itemContainer) return null;
                return itemContainer;
            }

            #endregion
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
