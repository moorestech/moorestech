using Client.Ui.State.Notification.Content;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 通知の行を足す・除く口。書く側は中継（Add）とホストの View（退場の終わりの Remove）の 2 つだけ
    /// Write side that adds and removes notification rows; the only writers are the relay (Add) and the host View (Remove at the exit end)
    /// </summary>
    public interface INotificationListWriter
    {
        void AddNotification(NotificationContent content);
        void RemoveNotification(long id);
    }
}
