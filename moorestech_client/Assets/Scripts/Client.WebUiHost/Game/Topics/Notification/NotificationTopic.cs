using System;
using Client.Game.InGame.Context;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.Notification;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    // notification.events トピック: サーバー通知イベントをWebへ中継する（揮発・スナップショット再生なし）
    // notification.events topic: relays server notification events to the web (transient, no snapshot replay)
    public sealed class NotificationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "notification.events";

        private readonly WebSocketHub _hub;
        private readonly IDisposable _subscription;
        private long _seq;

        public NotificationTopic(WebSocketHub hub)
        {
            _hub = hub;
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, OnNotification);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            // 通知は揮発。接続時に過去分を再生しない
            // Notifications are transient; do not replay history on connect
            return UniTask.FromResult("{}");
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
            if (!TryGetWebCategory(message.Category, out var webCategory))
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
                Count = message.Count,
            };
            _hub.Publish(TopicName, WebUiJson.Serialize(dto));

            #region Internal

            // Web側category名の唯一の対応表
            // The only mapping to web-side category names
            bool TryGetWebCategory(NotificationCategory category, out string name)
            {
                switch (category)
                {
                    case NotificationCategory.Achievement:
                        name = "achievement";
                        return true;
                    case NotificationCategory.OperationDenied:
                        name = "operationDenied";
                        return true;
                    case NotificationCategory.ItemEarned:
                        name = "itemEarned";
                        return true;
                    default:
                        name = null;
                        return false;
                }
            }

            #endregion
        }
    }

    public sealed class NotificationDto
    {
        public long Seq;
        public string Category;
        public string MessageId;
        public string[] MessageParams;
        public int? ItemId;
        public int Count;
    }
}
