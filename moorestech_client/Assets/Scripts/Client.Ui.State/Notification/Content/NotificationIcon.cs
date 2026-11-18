using Core.Master;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// 行の頭に出すアイコン。アイテムか無しかを型で表す（null で「無し」を表さない）
    /// The icon at the head of a row; the type says item or none (null never means "none")
    /// </summary>
    public abstract class NotificationIcon
    {
    }

    /// <summary>
    /// アイテムのアイコン
    /// An item icon
    /// </summary>
    public sealed class ItemNotificationIcon : NotificationIcon
    {
        public ItemId ItemId { get; }

        public ItemNotificationIcon(ItemId itemId)
        {
            ItemId = itemId;
        }
    }

    /// <summary>
    /// アイコン無し（除去告知・アイテムを伴わない実績と拒否）
    /// No icon (prune notices, achievements and denials without an item)
    /// </summary>
    public sealed class NoNotificationIcon : NotificationIcon
    {
        public static readonly NoNotificationIcon Instance = new();

        private NoNotificationIcon()
        {
        }
    }
}
