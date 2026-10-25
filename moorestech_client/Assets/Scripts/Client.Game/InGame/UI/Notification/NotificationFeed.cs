using System;
using Client.Network.API;
using MessagePack;
using Server.Event.Notification;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Notification
{
    /// <summary>
    /// サーバー通知とクライアント側の拒否を1本にまとめて連番を振る。接続時1回きりの移行告知だけは初期データとして持ち続ける
    /// Merges server notifications and client-side refusals into one numbered feed; only the once-per-connect migration notice is kept as initial data
    /// </summary>
    public sealed class NotificationFeed : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ClientLocalNotificationSource _localNotificationSource;
        private readonly Subject<SequencedNotification> _onNotified = new();
        private long _seq;

        // 除去件数の告知はサーバー接続時に1回しか来ない。描く側が未購読の間に流すと失われるので初期データとして持ち続ける
        // The prune notice arrives only once per server connection; it would be lost while a presenter is unsubscribed, so it is kept as initial data
        public SequencedNotification SaveMigrationNotice { get; private set; }
        public IObservable<SequencedNotification> OnNotified => _onNotified;

        public NotificationFeed(IVanillaApiEvent vanillaApiEvent, ClientLocalNotificationSource localNotificationSource)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _localNotificationSource = localNotificationSource;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(NotificationService.EventTag, OnServerNotification);

            // クライアント側の拒否も同じ表示へ流す
            // Relay client-side refusals through the same display
            _localNotificationSource.OnNotification.Subscribe(Publish);
        }

        private void OnServerNotification(byte[] payload)
        {
            Publish(MessagePackSerializer.Deserialize<NotificationMessagePack>(payload));
        }

        private void Publish(NotificationMessagePack message)
        {
            // 未定義のカテゴリは連番を消費せず落とす。throwは購読パイプを貫き配信を止める
            // Undefined categories are dropped without consuming a number; a throw would pierce the subscription pipe and halt delivery
            if (!Enum.IsDefined(typeof(NotificationCategory), message.Category))
            {
                Debug.LogWarning($"[NotificationFeed] dropped notification with unknown category: {message.Category}");
                return;
            }

            _seq++;
            var notification = new SequencedNotification(_seq, message);

            // 再接続で届いた新しい告知は新しい連番で置き換え、再接続のたびに出し直す契約を保つ
            // A notice from a reconnect replaces the old one with a new number, keeping the re-show-per-reconnect contract
            if (message.Category == NotificationCategory.SaveMigration) SaveMigrationNotice = notification;
            _onNotified.OnNext(notification);
        }
    }
}
