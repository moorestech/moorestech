using System;
using System.Collections.Generic;
using Core.Update;
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
        private readonly Dictionary<(int playerId, NotificationCategory category, string messageId), ulong> _lastSentTick = new();
        private uint _cooldownTicks = GameUpdater.SecondsToTicks(3);

        // クールダウン辞書への同時アクセスを防ぐロック
        // Lock guarding concurrent access to the cooldown dictionary
        private readonly object _lock = new();

        public NotificationService(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void SetCooldownDuration(TimeSpan cooldownDuration)
        {
            _cooldownTicks = GameUpdater.SecondsToTicks(cooldownDuration.TotalSeconds);
        }

        public void Notify(int playerId, NotificationMessagePack notification)
        {
            // 同一キーの連打はクールダウンで抑制しワイヤにスパムを乗せない
            // Suppress same-key bursts by cooldown so spam never reaches the wire
            if (!TryPassCooldown(playerId, notification)) return;
            _eventProtocolProvider.AddEvent(playerId, EventTag, MessagePackSerializer.Serialize(notification));
        }

        // 連打を落とさず全て届けたい通知の送信口。抑制するか否かは呼び出し側が決める
        // Send path for notifications that must all arrive; the caller decides whether bursts are suppressed
        public void NotifyWithoutCooldown(int playerId, NotificationMessagePack notification)
        {
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
            var now = GameUpdater.CurrentTick;
            // 判定と更新を1トランザクションとしてロックし競合更新を防ぐ
            // Lock the check-and-set as one transaction to prevent racing updates
            lock (_lock)
            {
                if (_lastSentTick.TryGetValue(key, out var last) && now - last < _cooldownTicks) return false;
                _lastSentTick[key] = now;
                return true;
            }
        }
    }
}
