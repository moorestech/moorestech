using System;
using Core.Master;
using MessagePack;

namespace Server.Event.Notification
{
    public enum NotificationCategory
    {
        Achievement,
        OperationDenied,
        ItemEarned,
    }

    [MessagePackObject]
    public class NotificationMessagePack
    {
        // Web側は別リテラルを持つので公開しない
        // The web keeps its own literal, so this stays private
        private const string ItemEarnedMessageId = "itemEarned.mined";

        // EventのMessagePackはProtocolMessagePackBaseを継承しない。Key(0)から開始
        // Event MessagePacks do not inherit ProtocolMessagePackBase; keys start at 0
        [Key(0)] public NotificationCategory Category { get; set; }
        [Key(1)] public string MessageId { get; set; }
        [Key(2)] public string[] MessageParams { get; set; }
        [Key(3)] public ItemId ItemId { get; set; }
        [Key(4)] public int Count { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public NotificationMessagePack() { }

        // 生成はstatic factory経由のみ。カテゴリごとの必要フィールドを型で明示する
        // Construction goes through static factories so each category's required fields are explicit
        private NotificationMessagePack(NotificationCategory category, string messageId, string[] messageParams, ItemId itemId, int count)
        {
            Category = category;
            MessageId = messageId;
            MessageParams = messageParams;
            ItemId = itemId;
            Count = count;
        }

        public static NotificationMessagePack CreateAchievement(string messageId, string[] messageParams)
            => new(NotificationCategory.Achievement, messageId, messageParams, ItemMaster.EmptyItemId, 0);

        public static NotificationMessagePack CreateAchievementWithItem(string messageId, string[] messageParams, ItemId itemId)
            => new(NotificationCategory.Achievement, messageId, messageParams, itemId, 0);

        public static NotificationMessagePack CreateOperationDenied(string messageId, string[] messageParams)
            => new(NotificationCategory.OperationDenied, messageId, messageParams, ItemMaster.EmptyItemId, 0);

        // 0個獲得・アイテム無しの獲得は存在しない結果なので生成時点で弾く
        // A zero-count or item-less earn is not a representable result, so it is rejected at construction
        public static NotificationMessagePack CreateItemEarned(ItemId itemId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, null);
            if (itemId == ItemMaster.EmptyItemId) throw new ArgumentException("itemId must not be empty", nameof(itemId));
            return new NotificationMessagePack(NotificationCategory.ItemEarned, ItemEarnedMessageId, Array.Empty<string>(), itemId, count);
        }
    }
}
