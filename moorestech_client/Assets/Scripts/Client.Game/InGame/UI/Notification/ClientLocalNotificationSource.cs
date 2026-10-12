using System;
using Server.Event.Notification;
using UniRx;

namespace Client.Game.InGame.UI.Notification
{
    /// <summary>
    ///     クライアント側だけで決まる拒否・取りこぼしを、サーバー通知と同じ通知表示へ流す発行元
    ///     Publisher pushing client-only refusals and skips onto the same notification display as server notifications
    /// </summary>
    public class ClientLocalNotificationSource
    {
        private readonly Subject<NotificationMessagePack> _onNotification = new();
        public IObservable<NotificationMessagePack> OnNotification => _onNotification;

        public void Notify(NotificationMessagePack message)
        {
            _onNotification.OnNext(message);
        }
    }
}
