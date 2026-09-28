using System;
using System.Collections.Generic;
using Game.PlayerConnection;
using Server.Event;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.Handshake;
using UnityEngine;

namespace Server.Boot.Replay
{
    // 記録順に接続を作成・解除し、同tick内の再接続でも古いcontextを再利用しない
    // Create and remove connections in record order, avoiding stale contexts on same-tick reconnects
    internal sealed class ReplayConnectionContexts
    {
        private readonly PlayerConnectionRegistry _connections;
        private readonly EventProtocolProvider _events;
        private readonly Dictionary<int, PacketResponseContext> _contexts = new();

        public ReplayConnectionContexts(PlayerConnectionRegistry connections, EventProtocolProvider events)
        {
            _connections = connections;
            _events = events;
        }

        public PacketResponseContext ContextFor(int? playerId)
        {
            // 未紐づけレコードは別接続かもしれないため、前のハンドシェイクを引き継がない
            // Unbound records may come from separate connections, so do not inherit an earlier handshake
            if (!playerId.HasValue) return new PacketResponseContext(null);
            if (_contexts.TryGetValue(playerId.Value, out var existing)) return existing;
            var created = new PacketResponseContext(null);
            if (!PlayerConnectionBinding.TryBind(playerId.Value, created, _connections, _events))
            {
                var reason = $"再生接続のバインドに失敗しました playerId:{playerId.Value}";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }
            _contexts.Add(playerId.Value, created);
            return created;
        }

        public void Disconnect(int? playerId)
        {
            // 未紐づけパケットは使い捨てcontextなので、null切断は解除する状態を持たない
            // Unbound packets use throwaway contexts, so a null disconnect has no state to remove
            if (!playerId.HasValue)
            {
                Debug.Log("再生した未紐づけ接続の切断には解除対象がありません");
                return;
            }
            _contexts.TryGetValue(playerId.Value, out var context);
            PlayerConnectionBinding.Unregister(playerId.Value, context?.EventSink, _connections, _events);
            _contexts.Remove(playerId.Value);
        }
    }
}
