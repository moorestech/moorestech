using System;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
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

        private readonly OpenableInventoryResolver _openableInventoryResolver;
        private readonly InventoryItemMoveRejectionReporter _rejectionReporter;

        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _openableInventoryResolver = serviceProvider.GetService<OpenableInventoryResolver>();
            _rejectionReporter = new InventoryItemMoveRejectionReporter(serviceProvider.GetService<NotificationService>());
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload);

            var fromInventory = GetInventory(data.FromInventoryIdentifier);
            if (fromInventory == null) return null;

            var fromSlot = data.FromSlot;

            var toInventory = GetInventory(data.ToInventoryIdentifier);
            if (toInventory == null) return null;

            var toSlot = data.ToSlot;

            switch (data.ItemMoveType)
            {
                case ItemMoveType.SwapSlot:
                    // 移動・無操作以外は拒否なので、識別子つきでログと通知へ出す
                    // Anything other than moved/no-op is a rejection, so report it with identifiers to the log and notification
                    var result = InventoryItemMoveService.Move(fromInventory, fromSlot, toInventory, toSlot, data.Count);
                    if (result != InventoryItemMoveResult.Moved && result != InventoryItemMoveResult.NoOp)
                    {
                        _rejectionReporter.Report(result, context.PlayerId, data.FromInventoryIdentifier, fromInventory, fromSlot, data.ToInventoryIdentifier, toInventory, toSlot, data.Count);
                    }
                    break;
                case ItemMoveType.InsertSlot:
                    InventoryItemInsertService.Insert(fromInventory, fromSlot, toInventory, data.Count);
                    break;
            }

            return null;
        }

        private IOpenableInventory GetInventory(InventoryIdentifierMessagePack inventoryIdentifier)
        {
            return _openableInventoryResolver.Resolve(inventoryIdentifier);
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
