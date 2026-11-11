using Client.Ui.State.Notification.Content;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の 1 行。合流すると Id を据え置いて LifetimeEpoch が進む（退場の状態は持たない）
    /// One visible row; a merge keeps the Id and advances LifetimeEpoch (no exit state is held)
    /// </summary>
    public sealed class NotificationRow
    {
        public long Id { get; }
        public int LifetimeEpoch { get; }
        public NotificationContent Content { get; }

        public NotificationRow(long id, int lifetimeEpoch, NotificationContent content)
        {
            Id = id;
            LifetimeEpoch = lifetimeEpoch;
            Content = content;
        }
    }
}
