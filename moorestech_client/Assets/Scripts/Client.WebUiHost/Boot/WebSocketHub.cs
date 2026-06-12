using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private readonly ConcurrentDictionary<Guid, Connection> _connections = new();
        private readonly ConcurrentDictionary<string, ITopicHandler> _handlers = new();
        private readonly ConcurrentDictionary<string, IActionHandler> _actionHandlers = new();

        // トピックハンドラ登録（InventoryTopic などが呼ぶ）
        // Register a topic handler (called by InventoryTopic etc.)
        public void RegisterTopic(string topic, ITopicHandler handler)
        {
            _handlers[topic] = handler;
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

        // 登録済みトピック・actionを全て解除。IDisposable なものは dispose する
        // Clear all registered topics and actions; dispose IDisposable handlers
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
        }

        // 全接続のうち指定トピックを購読している接続に event を配信
        // Broadcast an event payload to all connections subscribed to the topic
        public void Publish(string topic, string dataJson)
        {
            var envelope = BuildEnvelopeJson("event", topic, dataJson);
            foreach (var conn in _connections.Values)
            {
                if (conn.Topics.Contains(topic))
                {
                    conn.EnqueueSend(envelope);
                }
            }
        }

        // 新規接続を受け入れ、メッセージループを開始
        // Accept a new connection and start its message loop
        public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var conn = new Connection(webSocket);
            _connections[id] = conn;

            // 送信ループと受信ループを同時実行し、fault は完了時に必ずログへ残す
            // Run send/receive loops concurrently; faults are always logged on completion
            var sendTask = SendLoop(conn, ct);
            var receiveTask = ReceiveLoop(conn, ct);
            _ = sendTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] send loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            _ = receiveTask.ContinueWith(t => UnityEngine.Debug.LogWarning($"[WebSocketHub] receive loop faulted: {t.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
            await Task.WhenAny(sendTask, receiveTask);

            _connections.TryRemove(id, out _);
        }

        public async Task CloseAllAsync()
        {
            // 最大 2 秒タイムアウト付きで Close フレームを送信
            // Send Close frames with a 2-second timeout to avoid blocking forever
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            foreach (var conn in _connections.Values)
            {
                if (conn.WebSocket.State == WebSocketState.Open)
                {
                    await conn.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "server stopping", cts.Token);
                }
            }
            _connections.Clear();
        }

        private async Task ReceiveLoop(Connection conn, CancellationToken ct)
        {
            var buffer = new byte[8192];
            var messageBytes = new List<byte>();
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await conn.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await conn.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
                    return;
                }

                // フレーム断片を EndOfMessage まで蓄積してから1メッセージとして処理する
                // Accumulate frame fragments until EndOfMessage, then process as one message
                for (var i = 0; i < result.Count; i++) messageBytes.Add(buffer[i]);
                if (!result.EndOfMessage) continue;

                var json = Encoding.UTF8.GetString(messageBytes.ToArray());
                messageBytes.Clear();
                await HandleClientMessage(conn, json);
            }
        }

        private async Task HandleClientMessage(Connection conn, string json)
        {
            var msg = WebUiJson.Deserialize<WsClientMessage>(json);
            if (msg?.Op == null) return;

            switch (msg.Op)
            {
                case "subscribe":
                    if (msg.Topics == null) return;
                    foreach (var t in msg.Topics)
                    {
                        conn.Topics.Add(t);
                        await SendSnapshot(conn, t);
                    }
                    break;
                case "unsubscribe":
                    if (msg.Topics == null) return;
                    foreach (var t in msg.Topics) conn.Topics.Remove(t);
                    break;
                case "snapshot":
                    if (msg.Topic == null) return;
                    await SendSnapshot(conn, msg.Topic);
                    break;
                case "action":
                    await HandleActionAsync(conn, msg);
                    break;
            }
        }

        private async Task SendSnapshot(Connection conn, string topic)
        {
            if (!_handlers.TryGetValue(topic, out var handler)) return;

            // ゲーム状態を読むためメインスレッドでスナップショットを生成する
            // Build snapshots on the main thread because they read game state
            await UniTask.SwitchToMainThread();
            var snap = await handler.GetSnapshotJsonAsync();
            await UniTask.SwitchToTaskPool();

            conn.EnqueueSend(BuildEnvelopeJson("snapshot", topic, snap));
        }

        private async Task HandleActionAsync(Connection conn, WsClientMessage msg)
        {
            // requestId が無い action は応答相関できないため黙って捨てる
            // Drop actions without a requestId; the response cannot be correlated
            if (string.IsNullOrEmpty(msg.RequestId)) return;

            if (msg.Type == null || !_actionHandlers.TryGetValue(msg.Type, out var handler))
            {
                conn.EnqueueSend(BuildResultJson(msg.RequestId, false, "unknown_action"));
                return;
            }

            // ハンドラはゲーム状態に触るため必ずメインスレッドで実行する
            // Handlers touch game state, so always run them on the main thread
            await UniTask.SwitchToMainThread();

            // 停止処理と競合した場合はハンドラを実行しない（解体済みゲーム状態の保護）
            // Skip execution if shutdown cleared the registry while we awaited the main thread
            if (!_actionHandlers.ContainsKey(msg.Type))
            {
                await UniTask.SwitchToTaskPool();
                conn.EnqueueSend(BuildResultJson(msg.RequestId, false, "host_stopping"));
                return;
            }

            var result = await handler.ExecuteAsync(msg.Payload);
            await UniTask.SwitchToTaskPool();

            conn.EnqueueSend(BuildResultJson(msg.RequestId, result.Ok, result.Error));
        }

        private static string BuildResultJson(string requestId, bool ok, string error)
        {
            var env = new JObject
            {
                ["op"] = "result",
                ["requestId"] = requestId,
                ["ok"] = ok,
            };
            if (error != null) env["error"] = error;
            return env.ToString(Formatting.None);
        }

        private static string BuildEnvelopeJson(string op, string topic, string dataJson)
        {
            // 日付風文字列の暗黙DateTime変換を無効化し、データを素通しする
            // Disable implicit DateTime parsing so date-like strings pass through untouched
            var reader = new JsonTextReader(new StringReader(dataJson)) { DateParseHandling = DateParseHandling.None };
            var data = JToken.ReadFrom(reader);
            var env = new JObject
            {
                ["op"] = op,
                ["topic"] = topic,
                ["data"] = data,
            };
            return env.ToString(Formatting.None);
        }

        private async Task SendLoop(Connection conn, CancellationToken ct)
        {
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await conn.DequeueSendAsync(ct);
                var bytes = Encoding.UTF8.GetBytes(msg);
                await conn.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
        }

        /// <summary>
        /// 1 本の WS 接続の状態
        /// State of a single WS connection
        /// </summary>
        private sealed class Connection
        {
            public WebSocket WebSocket { get; }
            public HashSet<string> Topics { get; } = new();
            private readonly System.Threading.Channels.Channel<string> _sendChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

            public Connection(WebSocket webSocket)
            {
                WebSocket = webSocket;
            }

            public void EnqueueSend(string msg) => _sendChannel.Writer.TryWrite(msg);

            public async Task<string> DequeueSendAsync(CancellationToken ct)
            {
                return await _sendChannel.Reader.ReadAsync(ct);
            }
        }
    }
}
