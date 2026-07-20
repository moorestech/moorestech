using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client.Input;
using Client.WebUiHost.Game;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// トピックハンドラのインターフェース
    /// Interface for topic handlers
    /// </summary>
    public interface ITopicHandler
    {
        // 新規購読者に現在値を snapshot として返す
        // Return current value as snapshot for a new subscriber
        UniTask<string> GetSnapshotJsonAsync();
    }

    /// <summary>
    /// WS 接続の集約・購読管理・トピック配信
    /// Aggregates WS connections, manages subscriptions, dispatches topic events
    /// </summary>
    public class WebSocketHub
    {
        private readonly ConcurrentDictionary<Guid, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, ITopicHandler> _handlers = new();
        private readonly ConcurrentDictionary<string, IActionHandler> _actionHandlers = new();
        private readonly ConcurrentDictionary<string, long> _topicRevisions = new();
        private readonly WebSocketMessageDispatcher _dispatcher;

        public WebSocketHub()
        {
            _dispatcher = new WebSocketMessageDispatcher(_handlers, _actionHandlers, _topicRevisions);
        }

        // ページからのWS接続が1本でも確立しているか（CEFナビゲーション成否の確認に使う）
        // Whether at least one page WS connection is established (used to confirm CEF navigation)
        public bool HasConnections => !_connections.IsEmpty;

        // トピックハンドラ登録（InventoryTopic などが呼ぶ）
        // Register a topic handler (called by InventoryTopic etc.)
        public void RegisterTopic(string topic, ITopicHandler handler)
        {
            // 旧ハンドラがあれば Dispose してから差し替え、UniRx 購読リークを防ぐ（2-D）
            // Dispose any old handler before replacing to prevent UniRx subscription leaks (2-D)
            if (_handlers.TryGetValue(topic, out var existing) && !ReferenceEquals(existing, handler))
            {
                (existing as IDisposable)?.Dispose();
            }
            _handlers[topic] = handler;
            _topicRevisions.TryAdd(topic, 0);

            // 登録前に購読していた接続へ snapshot を再送する（connecting のまま/stale 表示を解消・2-E）
            // Re-send the snapshot to connections that subscribed before registration (fixes stuck-connecting/stale, 2-E)
            _dispatcher.BroadcastSnapshotAsync(topic, _connections.Values).Forget();
        }

        // action ハンドラ登録（WebUiGameBinder が呼ぶ）
        // Register an action handler (called by WebUiGameBinder)
        public void RegisterAction(IActionHandler handler)
        {
            if (!_actionHandlers.TryAdd(handler.ActionType, handler))
            {
                // 同名 ActionType の二重登録は実装ミスなので警告して上書きする
                // Duplicate ActionType registration is a programming error; warn and overwrite
                UnityEngine.Debug.LogWarning($"[WebSocketHub] duplicate action handler: {handler.ActionType}");
                _actionHandlers[handler.ActionType] = handler;
            }
        }

        // 全バインド解除。IDisposable は dispose
        // Clear all bindings; dispose IDisposable handlers
        public void ClearBindings()
        {
            foreach (var kv in _handlers)
            {
                (kv.Value as IDisposable)?.Dispose();
            }
            _handlers.Clear();

            foreach (var kv in _actionHandlers)
            {
                (kv.Value as IDisposable)?.Dispose();
            }
            _actionHandlers.Clear();

            // 保留中モーダルの await を放置しないよう cancel で解決する
            // Resolve any pending modal await with cancel so it is not left dangling
            WebUiModalService.Instance?.CancelPending();
        }

        // 全接続のうち指定トピックを購読している接続に event を配信
        // Broadcast an event payload to all connections subscribed to the topic
        public void Publish(string topic, string dataJson)
        {
            // revision はハンドラ再登録を跨いで維持し、ホスト生存中の単調増加を保証する
            // Preserve revision across handler rebinds to guarantee monotonicity for the host lifetime
            var revision = _topicRevisions.AddOrUpdate(topic, 1, (_, current) => checked(current + 1));
            var envelope = WebSocketEnvelope.BuildEnvelope("event", topic, revision, dataJson);
            foreach (var conn in _connections.Values)
            {
                if (conn.Topics.ContainsKey(topic))
                {
                    conn.EnqueueSend(envelope);
                }
            }
        }

        // 新規接続を受け入れ、送受信ループを開始
        // Accept a new connection and start its send/receive loops
        public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var conn = new WebSocketConnection(webSocket);
            _connections[id] = conn;

            // 送信ループと受信ループを同時実行し、fault は完了時に必ずログへ残す
            // Run send/receive loops concurrently; faults are always logged on completion
            var sendTask = conn.RunSendLoopAsync(ct);
            var receiveTask = ReceiveLoop(conn, ct);
            _ = sendTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] send loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            _ = receiveTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] receive loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            await Task.WhenAny(sendTask, receiveTask);

            // 片方のループが終わったら残るループも止めて接続を登録解除する
            // Once either loop ends, stop the other and unregister the connection
            conn.RequestStop();
            _connections.TryRemove(id, out _);
            if (_connections.IsEmpty) WebUiInputExclusivity.SetState(false, false);
        }

        public async Task CloseAllAsync()
        {
            // 最大 2 秒タイムアウト付きで Close フレームを送信
            // Send Close frames with a 2-second timeout to avoid blocking forever
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            foreach (var conn in _connections.Values)
            {
                // 1 接続の close 失敗が他接続の close を止めないよう境界で隔離する（2-B）
                // Isolate each close so one connection's failure cannot skip the rest (2-B)
                try
                {
                    await conn.CloseAsync(cts.Token);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[WebSocketHub] close failed: {e.GetBaseException().Message}");
                }
            }
            _connections.Clear();
        }

        private async Task ReceiveLoop(WebSocketConnection conn, CancellationToken ct)
        {
            var buffer = new byte[8192];
            var messageBytes = new List<byte>();
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await conn.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await conn.CloseAsync(cts.Token);
                    return;
                }

                // Text 以外（Binary 等）は Web UI プロトコル対象外なので破棄する（2-C）
                // Discard non-Text frames (Binary etc.); they are outside the Web UI protocol (2-C)
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    messageBytes.Clear();
                    continue;
                }

                // EndOfMessage まで断片を蓄積
                // Accumulate fragments until EndOfMessage
                for (var i = 0; i < result.Count; i++) messageBytes.Add(buffer[i]);
                if (!result.EndOfMessage) continue;

                var json = Encoding.UTF8.GetString(messageBytes.ToArray());
                messageBytes.Clear();
                await _dispatcher.DispatchAsync(conn, json);
            }
        }
    }
}
