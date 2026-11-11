using UniRx;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の通知の行を読む口（読む側は NotificationLayerPresenter）
    /// Read side of the visible notification rows (read by NotificationLayerPresenter)
    /// </summary>
    public interface INotificationListSource
    {
        IReadOnlyReactiveProperty<NotificationListViewModel> Current { get; }
    }
}
