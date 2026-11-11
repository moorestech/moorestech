using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Params;
using Core.Master;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// 通知 1 行の中身の基底。種別ごとの sealed な型が持つ項目を決める（数を持つのは獲得だけ。Web の NewGameNotification の判別 union）
    /// Base of one notification row's content; sealed per-kind types decide the fields (only earns carry a count; Web NewGameNotification union)
    /// </summary>
    public abstract class NotificationContent
    {
        public string MessageId { get; }
        public IReadOnlyList<NotificationParam> MessageParams { get; }
        public NotificationIcon Icon { get; }

        protected NotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon)
        {
            MessageId = messageId;
            MessageParams = messageParams;
            Icon = icon;
        }
    }

    /// <summary>
    /// アイテム獲得の中身。同じアイテム・同じ messageId の行へ数を足して合流する
    /// Content of an item earn; merges into a row with the same item and messageId by adding the count
    /// </summary>
    public sealed class EarnedNotificationContent : NotificationContent
    {
        public ItemId ItemId { get; }
        public int Count { get; }

        public EarnedNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, ItemId itemId, int count) : base(messageId, messageParams, new ItemNotificationIcon(itemId))
        {
            // 0 個以下の獲得は表せない結果なので作らせない（サーバーの CreateItemEarned と同じ約束）
            // A zero-or-less earn is not representable, so refuse it (same contract as the server CreateItemEarned)
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, null);
            ItemId = itemId;
            Count = count;
        }

        // 同じアイテム・同じ messageId なら数を足して合流した中身を返す。ほかの項目は新しい方を採る（Web の { ...n, count: merged.count + n.count }）
        // Returns the merged content with summed counts for the same item and messageId, taking the newer fields otherwise (Web { ...n, count: merged.count + n.count })
        public bool TryMergeWith(EarnedNotificationContent newer, out EarnedNotificationContent merged)
        {
            merged = null;
            if (newer.ItemId != ItemId || newer.MessageId != MessageId) return false;
            merged = new EarnedNotificationContent(newer.MessageId, newer.MessageParams, newer.ItemId, Count + newer.Count);
            return true;
        }
    }

    /// <summary>
    /// 実績（研究・チャレンジの完了、ブロック・アイテムの解放）の中身
    /// Content of an achievement (research or challenge completion, block or item unlock)
    /// </summary>
    public sealed class AchievementNotificationContent : NotificationContent
    {
        public AchievementNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon) : base(messageId, messageParams, icon)
        {
        }
    }

    /// <summary>
    /// 操作の拒否の中身。View はこの型を見て警告色で出す
    /// Content of a denied operation; the View shows this type in the warning color
    /// </summary>
    public sealed class OperationDeniedNotificationContent : NotificationContent
    {
        public OperationDeniedNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon) : base(messageId, messageParams, icon)
        {
        }
    }

    /// <summary>
    /// セーブの変換で除いた件数の告知。アイコンは持たない
    /// Notice of what the save migration pruned; it carries no icon
    /// </summary>
    public sealed class SaveMigrationNotificationContent : NotificationContent
    {
        public SaveMigrationNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams) : base(messageId, messageParams, NoNotificationIcon.Instance)
        {
        }
    }
}
