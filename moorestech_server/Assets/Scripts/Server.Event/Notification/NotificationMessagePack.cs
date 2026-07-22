using System;
using Core.Master;
using MessagePack;

namespace Server.Event.Notification
{
    public enum NotificationCategory
    {
        Achievement,
        OperationDenied,
    }

    [MessagePackObject]
    public class NotificationMessagePack
    {
        // EventのMessagePackはProtocolMessagePackBaseを継承しない。Key(0)から開始
        // Event MessagePacks do not inherit ProtocolMessagePackBase; keys start at 0
        [Key(0)] public NotificationCategory Category { get; set; }
        [Key(1)] public string MessageId { get; set; }
        [Key(2)] public string[] MessageParams { get; set; }
        [Key(3)] public ItemId ItemId { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public NotificationMessagePack() { }

        // 生成はstatic factory経由のみ。カテゴリごとの必要フィールドを型で明示する
        // Construction goes through static factories so each category's required fields are explicit
        private NotificationMessagePack(NotificationCategory category, string messageId, string[] messageParams, ItemId itemId)
        {
            Category = category;
            MessageId = messageId;
            MessageParams = messageParams;
            ItemId = itemId;
        }

        public static NotificationMessagePack CreateAchievement(string messageId, string[] messageParams)
            => new(NotificationCategory.Achievement, messageId, messageParams, ItemMaster.EmptyItemId);

        public static NotificationMessagePack CreateAchievementWithItem(string messageId, string[] messageParams, ItemId itemId)
            => new(NotificationCategory.Achievement, messageId, messageParams, itemId);

        public static NotificationMessagePack CreateOperationDenied(string messageId, string[] messageParams)
            => new(NotificationCategory.OperationDenied, messageId, messageParams, ItemMaster.EmptyItemId);
    }
}
