using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Text;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // 通知（GameUi）の登録。Feed は両方の系で MainGameUiStateRegistration が登録済み
    // Registers GameUi notifications; the feed is already registered for both systems by MainGameUiStateRegistration
    internal static class NotificationGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 1 つの Model を具体型（書く側：中継とホスト）と読み口（読む側：Presenter）で配る
            // Hand out one model as the concrete type (writers: relay and host) and the read interface (reader: presenter)
            builder.Register<NotificationModel>(Lifetime.Singleton).AsSelf().As<INotificationListSource>();
            builder.RegisterEntryPoint<NotificationFeedRelay>();
            builder.Register<LocalizeNotificationTextLookup>(Lifetime.Singleton).As<INotificationTextLookup>();
            builder.Register<GameContentNotificationRowIcons>(Lifetime.Singleton).As<INotificationRowIcons>();
            builder.RegisterEntryPoint<NotificationLayerPresenter>();
        }
    }
}
