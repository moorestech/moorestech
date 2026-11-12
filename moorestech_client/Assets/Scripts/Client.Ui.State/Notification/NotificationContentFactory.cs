using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;
using Server.Event.Notification;
using UnityEngine;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// サーバーの通知パケットを種別ごとの中身の型へ変換する。空のアイテム ID は「アイコン無し」の型へ閉じる（Web の NotificationTopic と同じ写し方）。作れない獲得は null を返す
    /// Converts a server notification packet into the per-kind content type; an empty item id closes into the "no icon" type (same mapping as the Web NotificationTopic); returns null for an unbuildable earn
    /// </summary>
    public static class NotificationContentFactory
    {
        // 戻り値は null を許す（0 個以下・空 ID の獲得は LogError を出して null。受けるのは NotificationFeedRelay の 1 か所）
        // The result may be null (a zero-or-less or empty-id earn logs an error and returns null; only NotificationFeedRelay receives it)
        public static NotificationContent FromMessage(NotificationMessagePack message)
        {
            // params はサーバーが null を送り得る外部データなので空で受ける
            // params are external data the server may send as null, so accept them as empty
            IReadOnlyDictionary<string, string> wireParams = message.MessageParams ?? new Dictionary<string, string>();
            var messageParams = NotificationParams.FromWire(message.MessageId, wireParams);

            // 外から来た表せない獲得は購読の中で throw させず、理由つきで落とす（落とすと以降の通知が止まるため）
            // Drop an unrepresentable external earn with a reason instead of throwing in the subscription, which would stop every later notification
            if (message.Category == NotificationCategory.ItemEarned && (message.Count < 1 || message.ItemId == ItemMaster.EmptyItemId))
            {
                Debug.LogError($"[NotificationContentFactory] dropped an unrepresentable item earn: itemId={message.ItemId.AsPrimitive()} count={message.Count} messageId={message.MessageId}");
                return null;
            }

            // 全カテゴリを列挙し、未知は止める（Feed が未定義値を先に落としているので到達しない）
            // List every category and stop on unknown (the feed already drops undefined values, so it is unreachable)
            return message.Category switch
            {
                NotificationCategory.ItemEarned => new EarnedNotificationContent(message.MessageId, messageParams, message.ItemId, message.Count),
                NotificationCategory.Achievement => new AchievementNotificationContent(message.MessageId, messageParams, IconOf(message.ItemId)),
                NotificationCategory.OperationDenied => new OperationDeniedNotificationContent(message.MessageId, messageParams, IconOf(message.ItemId)),
                NotificationCategory.SaveMigration => new SaveMigrationNotificationContent(message.MessageId, messageParams),
                _ => throw new ArgumentOutOfRangeException(nameof(message), message.Category, null),
            };

            #region Internal

            NotificationIcon IconOf(ItemId itemId)
            {
                return itemId == ItemMaster.EmptyItemId ? NoNotificationIcon.Instance : new ItemNotificationIcon(itemId);
            }

            #endregion
        }
    }
}
