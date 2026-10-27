using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Client.Input;
using Client.WebUiHost.Boot.Transport;
using Client.WebUiHost.Game;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Boot
{
    public interface ITopicHandler
    {
        // 新規購読者に現在値を snapshot として返す
        // Return current value as snapshot for a new subscriber
        UniTask<string> GetSnapshotJsonAsync();
    }

    public class WebSocketHub
    {
        private readonly ConcurrentDictionary<Guid, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, ITopicHandler> _handlers = new();
        private readonly ConcurrentDictionary<string, IActionHandler> _actionHandlers = new();
        private readonly ConcurrentDictionary<string, long> _topicRevisions = new();
        private readonly WebSocketMessageDispatcher _dispatcher;
        private readonly WebSocketReceiveLoop _receiver;
        public readonly ConnectionActionRegistry ConnectionActions;

        public WebSocketHub()
        {
            ConnectionActions = new ConnectionActionRegistry(this);
            _dispatcher = new WebSocketMessageDispatcher(this);
            _receiver = new WebSocketReceiveLoop(_dispatcher);
        }

        // 登録済みtopicハンドラの解決口。未登録はnull
        // Resolves a registered topic handler; null when unregistered
        public ITopicHandler ResolveTopic(string topic)
        {
            return _handlers.TryGetValue(topic, out var handler) ? handler : null;
        }

        // 登録済みactionハンドラの解決口。未登録はnull
        // Resolves a registered action handler; null when unregistered
        public IActionHandler ResolveAction(string actionType)
        {
            return _actionHandlers.TryGetValue(actionType, out var handler) ? handler : null;
        }

        // topicの現revision。未登録topicは0
        // Current revision of the topic; 0 for an unregistered topic
        public long GetTopicRevision(string topic)
        {
            return _topicRevisions.TryGetValue(topic, out var revision) ? revision : 0;
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
            if (ConnectionActions.Resolve(handler.ActionType) != null)
                throw new InvalidOperationException($"Action already registered with connection context: {handler.ActionType}");
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
            ConnectionActions.Clear();

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

        public bool HasConnection(Guid connectionId) => _connections.ContainsKey(connectionId);

        // 送信時にも購読を確認し、対象の1接続だけへ渡す
        // Check subscription at send time and deliver to exactly one connection
        public bool PublishTo(Guid connectionId, string topic, string dataJson)
        {
            if (!_connections.TryGetValue(connectionId, out var connection) || !connection.Topics.ContainsKey(topic))
            {
                UnityEngine.Debug.LogWarning($"[WebSocketHub] Directed delivery rejected: disconnected or unsubscribed ({topic})");
                return false;
            }
            var revision = _topicRevisions.AddOrUpdate(topic, 1, (_, current) => checked(current + 1));
            if (connection.EnqueueSend(WebSocketEnvelope.BuildEnvelope("event", topic, revision, dataJson))) return true;
            UnityEngine.Debug.LogWarning($"[WebSocketHub] Directed delivery rejected: connection stopping ({topic})");
            return false;
        }

        // 新規接続を受け入れ、送受信ループを開始
        // Accept a new connection and start its send/receive loops
        public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct, Guid id)
        {
            var conn = new WebSocketConnection(webSocket, id);
            _connections[id] = conn;

            // 送信ループと受信ループを同時実行し、fault は完了時に必ずログへ残す
            // Run send/receive loops concurrently; faults are always logged on completion
            var sendTask = conn.RunSendLoopAsync(ct);
            var receiveTask = _receiver.RunAsync(conn, ct);
            var actionTask = ActionLoop(conn, ct);
            _ = sendTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] send loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            _ = receiveTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] receive loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            _ = actionTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] action loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            await Task.WhenAny(sendTask, receiveTask);

            // 片方のループが終わったら残るループも止めて接続を登録解除する
            // Once either loop ends, stop the other and unregister the connection
            conn.RequestStop();
            _connections.TryRemove(id, out _);
            if (_connections.IsEmpty) WebUiInputExclusivity.SetState(false, false);
        }

        public async Task<WebSocketCloseResult> CloseAllAsync()
        {
            var result = await WebSocketConnectionCloser.CloseAllAsync(_connections.Values);
            _connections.Clear();
            return result;
        }

        // 受信ループと切り離して action を到着順に1本ずつ実行する。長い action が ping の読み出しを塞がないため
        // Runs actions one at a time in arrival order, off the receive loop, so a long action never blocks reading pings
        private async Task ActionLoop(WebSocketConnection conn, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await conn.ReadActionAsync(ct);
                if (msg == null) return;
                await _dispatcher.HandleActionAsync(conn, msg);
            }
        }
    }
}
