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

        // 区間開始時点で接続中だったIDを、世界を進める前に本番と同じ順序で登録する
        // Register the ids connected at the segment's start before advancing the world, in the same order production would
        public void PrewarmConnections(IReadOnlyCollection<int> connectedPlayerIds)
        {
            foreach (var playerId in connectedPlayerIds) ContextFor(playerId);
            Debug.Log($"再生: 区間開始時点の接続{connectedPlayerIds.Count}件を復元しました");
        }

        // 未紐づけパケットが再生中にハンドシェイクを通すと使い捨てcontextが紐づく。以後の同IDの記録はそのcontextを使う
        // A replayed unbound packet can complete a handshake and bind its throwaway context; later records for that id reuse it
        public void AdoptBoundContext(PacketResponseContext context)
        {
            if (!context.PlayerId.HasValue) return;
            if (_contexts.ContainsKey(context.PlayerId.Value)) return;
            _contexts.Add(context.PlayerId.Value, context);
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
            // 区間ヘッダの接続集合が剪定・旧版で欠けていると解除対象を持たない。読み側の警告と重大度を揃えて縮退する
            // A pruned or older segment header leaves nothing to remove here; degrade at the same severity the reader uses
            if (!_contexts.TryGetValue(playerId.Value, out var context))
            {
                Debug.LogError($"再生: playerId {playerId.Value} の接続が未作成のため解除対象がありません（区間ヘッダの接続集合が欠けている可能性があります）");
            }
            PlayerConnectionBinding.Unregister(playerId.Value, context?.EventSink, _connections, _events);
            _contexts.Remove(playerId.Value);
        }
    }
}
