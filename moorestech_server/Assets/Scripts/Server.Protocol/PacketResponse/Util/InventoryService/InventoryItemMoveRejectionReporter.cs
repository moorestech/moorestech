using System;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.Inventory;
using Server.Event.Notification;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    /// <summary>
    ///     移動拒否を識別子つきの1本のログとプレイヤー通知へまとめる。移動プロトコルは片道で応答が無いため、ここが唯一の拒否の出口
    ///     Folds a move rejection into one identifier-rich log line and a player notification; the move protocol is one-way, so this is the only rejection outlet
    /// </summary>
    public class InventoryItemMoveRejectionReporter
    {
        private readonly NotificationService _notificationService;

        public InventoryItemMoveRejectionReporter(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public void Report(InventoryItemMoveResult result, int? playerId, InventoryIdentifierMessagePack fromIdentifier, IOpenableInventory fromInventory, int fromSlot, InventoryIdentifierMessagePack toIdentifier, IOpenableInventory toInventory, int toSlot, int requestedCount)
        {
            // 拒否は書き込みを伴わないので、現在の中身がそのまま拒否されたスタックになる
            // A rejection writes nothing, so the current contents are exactly the rejected stacks
            var fromItem = fromInventory.GetItem(fromSlot);
            var toItem = toInventory.GetItem(toSlot);
            var toReason = DescribePlacement(toInventory, toSlot, fromItem);
            var fromReason = DescribePlacement(fromInventory, fromSlot, toItem);
            Debug.LogWarning($"[InventoryItemMoveProtocol] Move rejected: result={result} player={playerId?.ToString() ?? "unbound"} from={FormatIdentifier(fromIdentifier)}[{fromSlot}] fromItemId={fromItem.Id} fromCount={fromItem.Count} fromReason={fromReason} to={FormatIdentifier(toIdentifier)}[{toSlot}] toItemId={toItem.Id} toCount={toItem.Count} toReason={toReason} requestedCount={requestedCount}");

            // 接続にプレイヤーが紐付いていなければ通知先が無いのでログのみ
            // Without a player bound to the connection there is no notification target, so only the log remains
            if (!playerId.HasValue) return;
            if (result == InventoryItemMoveResult.RejectedPartialSwap)
            {
                _notificationService.Notify(playerId.Value, NotificationMessagePack.CreateOperationDenied("denied.inventoryMovePartialSwap", Array.Empty<string>()));
                return;
            }
            _notificationService.Notify(playerId.Value, NotificationMessagePack.CreateOperationDenied("denied.inventoryMoveSlotRejected", Array.Empty<string>()));

            #region Internal

            // 拒否しうるのはレシピ束縛を持つ機械だけなので、機械なら束縛の拒否理由まで出す
            // Only recipe-bound machines can refuse, so machines report the binding's rejection reason
            string DescribePlacement(IOpenableInventory inventory, int slot, IItemStack itemStack)
            {
                if (inventory is VanillaMachineBlockInventoryComponent machineInventory) return machineInventory.CheckPlacement(slot, itemStack).ToString();
                return inventory.IsAllowedToPlace(slot, itemStack) ? "Allowed" : "NotAllowed";
            }

            string FormatIdentifier(InventoryIdentifierMessagePack identifier)
            {
                return identifier.InventoryType switch
                {
                    InventoryType.Block => $"Block{identifier.BlockPosition}",
                    InventoryType.Train => $"Train({identifier.TrainCarInstanceId})",
                    _ => $"{identifier.InventoryType}(player={identifier.PlayerId})",
                };
            }

            #endregion
        }
    }
}
