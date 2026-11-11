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
            // 1 つの Model を読み口と書き口の 2 つの型で配る（読むのは Presenter、書くのは中継とホスト）
            // Hand out one model under the read and write interfaces (the presenter reads; the relay and host write)
            builder.Register<NotificationModel>(Lifetime.Singleton).As<INotificationListSource>().As<INotificationListWriter>();
            builder.RegisterEntryPoint<NotificationFeedRelay>();
            builder.Register<LocalizeNotificationTextLookup>(Lifetime.Singleton).As<INotificationTextLookup>();
            builder.Register<GameContentNotificationRowIcons>(Lifetime.Singleton).As<INotificationRowIcons>();
            builder.RegisterEntryPoint<NotificationLayerPresenter>();
        }
    }
}
