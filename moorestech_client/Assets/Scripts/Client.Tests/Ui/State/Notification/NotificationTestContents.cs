using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// テストで使う通知の中身の作り方。受信の引数は本番と同じ FromWire で型付きにする
    /// Builders for the notification contents used by tests; wire params become typed through the production FromWire
    /// </summary>
    internal static class NotificationTestContents
    {
        public static readonly IReadOnlyList<NotificationParam> NoParams = Array.Empty<NotificationParam>();
        public static readonly NotificationIcon NoIcon = NoNotificationIcon.Instance;

        public static NotificationIcon ItemIcon(int itemId)
        {
            return new ItemNotificationIcon(new ItemId(itemId));
        }

        public static IReadOnlyList<NotificationParam> Wire(string messageId, Dictionary<string, string> wireParams)
        {
            return NotificationParams.FromWire(messageId, wireParams);
        }

        public static EarnedNotificationContent Earned(int itemId, int count, string messageId)
        {
            return new EarnedNotificationContent(messageId, NoParams, new ItemId(itemId), count);
        }

        public static AchievementNotificationContent Achievement(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon)
        {
            return new AchievementNotificationContent(messageId, messageParams, icon);
        }

        public static OperationDeniedNotificationContent Denied(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon)
        {
            return new OperationDeniedNotificationContent(messageId, messageParams, icon);
        }

        public static SaveMigrationNotificationContent SaveMigration(string messageId, IReadOnlyList<NotificationParam> messageParams)
        {
            return new SaveMigrationNotificationContent(messageId, messageParams);
        }
    }
}
