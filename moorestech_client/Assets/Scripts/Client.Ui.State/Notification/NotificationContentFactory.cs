using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;
using Server.Event.Notification;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// サーバーの通知パケットを種別ごとの中身の型へ変換する。空のアイテム ID は「アイコン無し」の型へ閉じる（Web の NotificationTopic と同じ写し方）
    /// Converts a server notification packet into the per-kind content type; an empty item id closes into the "no icon" type (same mapping as the Web NotificationTopic)
    /// </summary>
    public static class NotificationContentFactory
    {
        public static NotificationContent FromMessage(NotificationMessagePack message)
        {
            // params はサーバーが null を送り得る外部データなので空で受ける
            // params are external data the server may send as null, so accept them as empty
            IReadOnlyDictionary<string, string> wireParams = message.MessageParams ?? new Dictionary<string, string>();
            var messageParams = NotificationParams.FromWire(message.MessageId, wireParams);

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
