using System;
using Client.Network.API;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.Notification;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    // notification.events トピック: サーバー通知イベントをWebへ中継する。接続時1回きりの告知だけはsnapshotで再提示する
    // notification.events topic: relays server notifications to the web; only the once-per-connect notice is re-served in the snapshot
    public sealed class NotificationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "notification.events";

        private readonly WebSocketHub _hub;
        private readonly IDisposable _subscription;
        private long _seq;

        // 除去件数の告知はサーバー接続時に1回しか来ない。Web未購読の間に配ると失われるので初期データとして持ち続ける
        // The prune notice arrives only once per server connection; publishing it while the web is unsubscribed loses it, so it is kept as initial data
        private NotificationDto _saveMigrationNotice;

        public NotificationTopic(WebSocketHub hub, IVanillaApiEvent vanillaApiEvent)
        {
            _hub = hub;
            _subscription = vanillaApiEvent.SubscribeEventResponse(NotificationService.EventTag, OnNotification);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            // 通常の通知は揮発で再生しない。除去件数の告知だけを返し、同じseqの再表示はWeb側が弾く
            // Ordinary notifications are transient; only the prune notice is returned and the web drops a repeat of the same seq
            var notice = _saveMigrationNotice;
            return UniTask.FromResult(notice == null ? "{}" : WebUiJson.Serialize(notice));
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void OnNotification(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<NotificationMessagePack>(payload);

            // throwは購読パイプを貫き配信を止める
            // A throw would pierce the subscription pipe and halt all event delivery
            if (!NotificationCategoryTable.TryGetWebName(message.Category, out var webCategory))
            {
                Debug.LogWarning($"[NotificationTopic] dropped notification with unknown category: {message.Category}");
                return;
            }

            _seq++;
            var dto = new NotificationDto
            {
                Seq = _seq,
                Category = webCategory,
                MessageId = message.MessageId,
                MessageParams = message.MessageParams,
                ItemId = message.ItemId == ItemMaster.EmptyItemId ? null : (int?)message.ItemId.AsPrimitive(),
                // countは獲得通知だけが持つ。他カテゴリはキーごと省略しWeb側の判別unionを保つ
                // Only earned notifications carry a count; other categories omit the key entirely to keep the web's discriminated union honest
                Count = message.Category == NotificationCategory.ItemEarned ? message.Count : (int?)null,
            };

            // 再接続で届いた新しい告知は新しいseqで置き換え、再接続のたびに出し直す契約を保つ
            // A notice from a reconnect replaces the old one with a new seq, keeping the re-show-per-reconnect contract
            if (message.Category == NotificationCategory.SaveMigration) _saveMigrationNotice = dto;
            _hub.Publish(TopicName, WebUiJson.Serialize(dto));
        }
    }

    public sealed class NotificationDto
    {
        public long Seq;
        public string Category;
        public string MessageId;
        public string[] MessageParams;
        public int? ItemId;
        public int? Count;
    }
}
