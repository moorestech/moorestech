using System;
using System.Collections.Generic;
using Client.Ui.State.Notification;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Event.Notification;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    public sealed class NotificationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "notification.events";

        private readonly WebSocketHub _hub;
        private readonly NotificationFeed _feed;
        private readonly IDisposable _subscription;

        public NotificationTopic(WebSocketHub hub, NotificationFeed feed)
        {
            _hub = hub;
            _feed = feed;
            _subscription = _feed.OnNotified.Subscribe(Publish);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            // 通常の通知は揮発で再生しない。除去件数の告知だけを返し、同じseqの再表示はWeb側が弾く
            // Ordinary notifications are transient; only the prune notice is returned and the web drops a repeat of the same seq
            var notice = _feed.SaveMigrationNotice;
            if (notice == null || !TrySerialize(notice, out var json)) return UniTask.FromResult("{}");
            return UniTask.FromResult(json);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void Publish(SequencedNotification notification)
        {
            if (TrySerialize(notification, out var json)) _hub.Publish(TopicName, json);
        }

        // 未定義値はfeedが既に落とす
        // Undefined values are already dropped by the feed
        private static bool TrySerialize(SequencedNotification notification, out string json)
        {
            var message = notification.Message;
            if (!NotificationCategoryTable.TryGetWebName(message.Category, out var webCategory))
            {
                Debug.LogError($"[NotificationTopic] no web category name for {message.Category}; notification seq {notification.Seq} was not sent");
                json = null;
                return false;
            }
            json = WebUiJson.Serialize(ToDto(webCategory));
            return true;

            #region Internal

            NotificationDto ToDto(string webCategory)
            {
                return new NotificationDto
                {
                    Seq = notification.Seq,
                    Category = webCategory,
                    MessageId = message.MessageId,
                    MessageParams = message.MessageParams,
                    ItemId = message.ItemId == ItemMaster.EmptyItemId ? null : (int?)message.ItemId.AsPrimitive(),
                    // countは獲得通知のみ
                    // Only earned notifications carry count
                    Count = message.Category == NotificationCategory.ItemEarned ? message.Count : (int?)null,
                };
            }

            #endregion
        }
    }

    public sealed class NotificationDto
    {
        public long Seq;
        public string Category;
        public string MessageId;
        public Dictionary<string, string> MessageParams;
        public int? ItemId;
        public int? Count;
    }
}
