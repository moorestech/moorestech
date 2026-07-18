using System;
using System.Collections.Generic;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;

namespace Server.Event
{
    /// <summary>
    ///     サーバー内で起こったイベントを、接続済みプレイヤーのsinkへ即時配信します。
    ///     Immediately dispatches server events to registered per-player sinks.
    /// </summary>
    public class EventProtocolProvider
    {
        private readonly Dictionary<int, IPlayerEventSink> _sinks = new();
        private readonly object _lock = new();
        private readonly Subject<int> _playerEventStreamRegistered = new();

        // sink登録完了直後に発火。購読者は同期的にAddEventすること（初期同期の順序契約）
        // Fires right after registration. Subscribers must AddEvent synchronously (ordering contract).
        public IObservable<int> OnPlayerEventStreamRegistered => _playerEventStreamRegistered;

        public void RegisterPlayer(int playerId, IPlayerEventSink sink)
        {
            // sink未配線のテスト経路ではイベント購読なしとして扱う（本番はacceptorが必ずセットする）
            // Treat null sinks (test-only paths) as no subscription; production always sets one
            if (sink == null) return;

            lock (_lock)
            {
                _sinks[playerId] = sink;
            }

            // 登録完了後に発火し、購読者が初期イベントを同期pushできるようにする
            // Fire after registration so subscribers can push initial events synchronously
            _playerEventStreamRegistered.OnNext(playerId);
        }

        public void UnregisterPlayer(int playerId, IPlayerEventSink sink)
        {
            lock (_lock)
            {
                // 現役sinkと一致する場合のみ解除。同一playerId再接続後に旧接続の切断が新sinkを壊さないため
                // Remove only the matching sink so a stale disconnect never clobbers a reconnected sink
                if (_sinks.TryGetValue(playerId, out var current) && ReferenceEquals(current, sink))
                {
                    _sinks.Remove(playerId);
                }
            }
        }

        public void AddEvent(int playerId, string tag, byte[] payload)
        {
            lock (_lock)
            {
                // 未接続プレイヤー宛は破棄する（handshakeで全量を取り直すため正しい）
                // Drop events for unconnected players; they fully re-sync on handshake
                if (_sinks.TryGetValue(playerId, out var sink))
                {
                    sink.EnqueueEvent(new EventMessagePack(tag, payload));
                }
            }
        }

        public void AddBroadcastEvent(string tag, byte[] payload)
        {
            lock (_lock)
            {
                var eventMessagePack = new EventMessagePack(tag, payload);
                foreach (var sink in _sinks.Values) sink.EnqueueEvent(eventMessagePack);
            }
        }
    }
    
    
    [MessagePackObject]
    public class EventMessagePack
    {
        public EventMessagePack(string tag, byte[] payload)
        {
            Tag = tag;
            Payload = payload;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EventMessagePack()
        {
        }
        
        [Key(0)] public string Tag { get; set; }
        
        [Key(1)] public byte[] Payload { get; set; }
        
        [Key(2)] public Dictionary<string,BlockStateMessagePack> MessagePacks { get; set; }
    }
}
