using System;
using Server.Event.Notification;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 通知の Feed を購読し、中身へ変換して書き口へ積む。購読前に来た除去告知は初期データから 1 回だけ積む
    /// Subscribes the notification feed and pushes converted contents through the write side; a prune notice that came before subscribing is pushed once from the initial data
    /// </summary>
    public sealed class NotificationFeedRelay : IInitializable, IDisposable
    {
        private readonly NotificationFeed _feed;
        private readonly INotificationListWriter _writer;
        private IDisposable _subscription;

        public NotificationFeedRelay(NotificationFeed feed, INotificationListWriter writer)
        {
            _feed = feed;
            _writer = writer;
        }

        public void Initialize()
        {
            // 初期データと購読を同じ同期区間で取るので、間に来た通知の取りこぼしも二重もない
            // Initial data and the subscription are taken in one synchronous span, so nothing is missed or doubled in between
            var initialNotice = _feed.SaveMigrationNotice;
            if (initialNotice != null) Push(initialNotice.Message);
            _subscription = _feed.OnNotified.Subscribe(notification => Push(notification.Message));

            #region Internal

            void Push(NotificationMessagePack message)
            {
                _writer.AddNotification(NotificationContentFactory.FromMessage(message));
            }

            #endregion
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
