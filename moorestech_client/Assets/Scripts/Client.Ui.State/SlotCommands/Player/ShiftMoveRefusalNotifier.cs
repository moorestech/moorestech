using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Notification;
using Client.Ui.State.ItemMove;
using Core.Master;
using Server.Event.Notification;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// Shift の配分を送らない理由を、Web と同じ messageId の通知と開発者ログに揃える（Web notifyBlockedShift の写し）
    /// Publishes why a Shift allocation is not sent as a notification with the Web messageId and a developer log (port of Web notifyBlockedShift)
    /// </summary>
    public sealed class ShiftMoveRefusalNotifier
    {
        private readonly ClientLocalNotificationSource _notifications;

        public ShiftMoveRefusalNotifier(ClientLocalNotificationSource notifications)
        {
            _notifications = notifications;
        }

        public void Notify(ShiftMoveRefusalReason reason, ItemId itemId, string blockIdentifier)
        {
            Debug.LogWarning($"[inventory.shift_move] not sent: {ToLogName(reason)} block={blockIdentifier} item={itemId}");

            // 宛先が満杯は Web に無い断り。messageId を持たないので通知せず、上のログだけで開発者に知らせる（D-9）
            // Destination-full has no Web counterpart and no messageId; no notification, the log above tells developers (D-9)
            // silent-ok: DestinationFull は Web も無音 / 上の Warning ログで開発者が気づく
            if (!TryGetMessageId(reason, out var messageId)) return;
            _notifications.Notify(NotificationMessagePack.CreateOperationDenied(messageId, new Dictionary<string, string>()));

            #region Internal

            bool TryGetMessageId(ShiftMoveRefusalReason refusal, out string id)
            {
                switch (refusal)
                {
                    case ShiftMoveRefusalReason.NoAcceptingBlockSlot:
                        id = "denied.inventoryMoveSlotRejected";
                        return true;
                    case ShiftMoveRefusalReason.InventoryOpenFailed:
                        id = "denied.inventoryOpenFailed";
                        return true;
                    case ShiftMoveRefusalReason.DetailLoading:
                        id = "denied.inventoryDetailLoading";
                        return true;
                    case ShiftMoveRefusalReason.DestinationFull:
                        // silent-ok: DestinationFull は Web に messageId が無い / 呼び手の Warning ログで開発者が気づく
                        id = "";
                        return false;
                    case ShiftMoveRefusalReason.UnknownItem:
                        // 未知の ID は呼び手が ItemUnknownInSlot の拒否で返す。ここへ来たら配線の誤り（D-65）
                        // Callers return an ItemUnknownInSlot rejection for an unknown ID; reaching here is a wiring mistake (D-65)
                        throw new ArgumentException("UnknownItem は通知でなく ItemUnknownInSlot の拒否で返す", nameof(refusal));
                    default:
                        // enum に値を足して対応を足し忘れた開発時の誤り（D-44 と同じ扱い）
                        // A dev-time mistake: a new enum value without a mapping (same as D-44)
                        throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null);
                }
            }

            #endregion
        }

        // 断り理由のログ名（Web の reason 文字列と同じ camelCase）。プレイヤー側とブロック側のログで共有する
        // Log name of a refusal reason (camelCase like the Web reason string), shared by player-side and block-side logs
        internal static string ToLogName(ShiftMoveRefusalReason refusal)
        {
            return refusal switch
            {
                ShiftMoveRefusalReason.NoAcceptingBlockSlot => "noAcceptingBlockSlot",
                ShiftMoveRefusalReason.InventoryOpenFailed => "inventoryOpenFailed",
                ShiftMoveRefusalReason.DetailLoading => "detailLoading",
                ShiftMoveRefusalReason.DestinationFull => "destinationFull",
                ShiftMoveRefusalReason.UnknownItem => "unknownItem",
                _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null),
            };
        }
    }
}
