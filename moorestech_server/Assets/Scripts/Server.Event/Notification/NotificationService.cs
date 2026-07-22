using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Event.Notification
{
    /// <summary>
    /// 汎用通知送信サービス
    /// Generic service that sends notifications
    /// </summary>
    public class NotificationService
    {
        public const string EventTag = "va:event:notification";

        // ブロードキャスト用擬似プレイヤーID
        // Pseudo player id for broadcasts
        private const int BroadcastPlayerId = -1;

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly Dictionary<(int playerId, NotificationCategory category, string messageId), DateTime> _lastSentUtc = new();
        private TimeSpan _cooldownDuration = TimeSpan.FromSeconds(3);

        // クールダウン辞書への同時アクセスを防ぐロック
        // Lock guarding concurrent access to the cooldown dictionary
        private readonly object _lock = new();

        public NotificationService(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void SetCooldownDuration(TimeSpan cooldownDuration)
        {
            _cooldownDuration = cooldownDuration;
        }

        public void Notify(int playerId, NotificationMessagePack notification)
        {
            // 同一キーの連打はクールダウンで抑制しワイヤにスパムを乗せない
            // Suppress same-key bursts by cooldown so spam never reaches the wire
            if (!TryPassCooldown(playerId, notification)) return;
            _eventProtocolProvider.AddEvent(playerId, EventTag, MessagePackSerializer.Serialize(notification));
        }

        public void NotifyAll(NotificationMessagePack notification)
        {
            if (!TryPassCooldown(BroadcastPlayerId, notification)) return;
            _eventProtocolProvider.AddBroadcastEvent(EventTag, MessagePackSerializer.Serialize(notification));
        }

        private bool TryPassCooldown(int playerId, NotificationMessagePack notification)
        {
            var key = (playerId, notification.Category, notification.MessageId);
            var now = DateTime.UtcNow;
            // 判定と更新を1トランザクションとしてロックし競合更新を防ぐ
            // Lock the check-and-set as one transaction to prevent racing updates
            lock (_lock)
            {
                if (_lastSentUtc.TryGetValue(key, out var last) && now - last < _cooldownDuration) return false;
                _lastSentUtc[key] = now;
                return true;
            }
        }
    }
}
