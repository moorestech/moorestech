using System;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
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
        private readonly InventoryItemMoveRejectionCorrector _rejectionCorrector;

        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _openableInventoryResolver = serviceProvider.GetService<OpenableInventoryResolver>();
            _rejectionReporter = new InventoryItemMoveRejectionReporter(serviceProvider.GetService<NotificationService>());
            _rejectionCorrector = new InventoryItemMoveRejectionCorrector(serviceProvider.GetService<EventProtocolProvider>());
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload);

            var fromInventory = GetInventory(data.FromInventoryIdentifier, requesterPlayerId);
            var fromSlot = data.FromSlot;
            var toInventory = GetInventory(data.ToInventoryIdentifier, requesterPlayerId);
            var toSlot = data.ToSlot;

            // 片側を解決できない移動（削除済みブロック等）は拒否し、解決できた側の先行表示だけを要求者へ訂正する
            // A move with an unresolvable side (e.g. a removed block) is rejected; only the resolved side's optimistic display is corrected for the requester
            if (fromInventory == null || toInventory == null)
            {
                _rejectionReporter.ReportUnresolved(requesterPlayerId, data.FromInventoryIdentifier, fromInventory != null, fromSlot, data.ToInventoryIdentifier, toInventory != null, toSlot, data.Count);
                _rejectionCorrector.Resend(requesterPlayerId, data.FromInventoryIdentifier, fromInventory, fromSlot, data.ToInventoryIdentifier, toInventory, toSlot);
                return null;
            }

            switch (data.ItemMoveType)
            {
                case ItemMoveType.SwapSlot:
                    // 移動・無操作以外は拒否なので、識別子つきでログと通知へ出し、要求者の先行表示を現在値へ戻す
                    // Anything other than moved/no-op is a rejection: report it with identifiers and restore the requester's optimistic display to the current values
                    var result = InventoryItemMoveService.Move(fromInventory, fromSlot, toInventory, toSlot, data.Count);
                    if (result != InventoryItemMoveResult.Moved && result != InventoryItemMoveResult.NoOp)
                    {
                        _rejectionReporter.Report(result, requesterPlayerId, data.FromInventoryIdentifier, fromInventory, fromSlot, data.ToInventoryIdentifier, toInventory, toSlot, data.Count);
                        _rejectionCorrector.Resend(requesterPlayerId, data.FromInventoryIdentifier, fromInventory, fromSlot, data.ToInventoryIdentifier, toInventory, toSlot);
                    }
                    break;
                case ItemMoveType.InsertSlot:
                    InventoryItemInsertService.Insert(fromInventory, fromSlot, toInventory, data.Count);
                    break;
            }

            return null;
        }

        private IOpenableInventory GetInventory(InventoryIdentifierMessagePack inventoryIdentifier, int requesterPlayerId)
        {
            return _openableInventoryResolver.Resolve(inventoryIdentifier, requesterPlayerId);
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
