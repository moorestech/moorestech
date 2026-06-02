using System;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
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

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload);

            var fromInventory = GetInventory(data.FromInventoryIdentifier);
            if (fromInventory == null) return null;

            var fromSlot = data.FromSlot;
            if (data.FromInventoryIdentifier.InventoryType.IsSubInventory())
                fromSlot -= PlayerInventoryConst.MainInventorySize;


            var toInventory = GetInventory(data.ToInventoryIdentifier);
            if (toInventory == null) return null;

            var toSlot = data.ToSlot;
            if (data.ToInventoryIdentifier.InventoryType.IsSubInventory())
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

        private IOpenableInventory GetInventory(InventoryIdentifierMessagePack inventoryIdentifier)
        {
            return OpenableInventoryResolver.Resolve(inventoryIdentifier, _playerInventoryDataStore, _trainUnitLookupDatastore);
        }

        [MessagePackObject]
        public class InventoryItemMoveProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int Count { get; set; }
            [Key(3)] public ItemMoveType ItemMoveType { get; set; }
            [Key(4)] public InventoryIdentifierMessagePack FromInventoryIdentifier { get; set; }
            [Key(5)] public int FromSlot { get; set; }
            [Key(6)] public InventoryIdentifierMessagePack ToInventoryIdentifier { get; set; }
            [Key(7)] public int ToSlot { get; set; }


            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public InventoryItemMoveProtocolMessagePack() { }
            public InventoryItemMoveProtocolMessagePack(int count, ItemMoveType itemMoveType,
                InventoryIdentifierMessagePack fromInventoryIdentifier, int fromSlot,
                InventoryIdentifierMessagePack toInventoryIdentifier, int toSlot)
            {
                Tag = ProtocolTag;
                Count = count;

                ItemMoveType = itemMoveType;
                FromInventoryIdentifier = fromInventoryIdentifier;
                FromSlot = fromSlot;
                ToInventoryIdentifier = toInventoryIdentifier;
                ToSlot = toSlot;
            }
        }
    }
}
