using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

        // トピックハンドラ登録（InventoryTopic などが呼ぶ）
        // Register a topic handler (called by InventoryTopic etc.)
        public void RegisterTopic(string topic, ITopicHandler handler)
        {
            _handlers[topic] = handler;
        }

        // 固定 JSON スナップショットでトピックを登録（テスト・デバッグ用）
        // Register a topic with a fixed JSON snapshot (for testing/debugging)
        public void RegisterStaticTopic(string topic, string snapshotJson)
        {
            _handlers[topic] = new StaticTopicHandler(snapshotJson);
        }

        // 固定 JSON を返す最小トピックハンドラ
        // Minimal topic handler that returns a fixed JSON string
        private sealed class StaticTopicHandler : ITopicHandler
        {
            private readonly string _json;
            public StaticTopicHandler(string json) { _json = json; }
            public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(_json);
        }

        // 全接続のうち指定トピックを購読している接続に event を配信
        // Broadcast an event payload to all connections subscribed to the topic
        public void Publish(string topic, string dataJson)
        {
            var envelope = $"{{\"op\":\"event\",\"topic\":\"{EscapeJsonString(topic)}\",\"data\":{dataJson}}}";
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
            var conn = new Connection(id, webSocket);
            _connections[id] = conn;

            // 送信ループと受信ループを同時実行
            // Run send loop and receive loop concurrently
            var sendTask = SendLoop(conn, ct);
            var receiveTask = ReceiveLoop(conn, ct);
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
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                // クライアント切断例外を捕捉してループを安全に終了
                // Catch WebSocket exceptions on abrupt client disconnect and exit safely
                result = await conn.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await conn.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
                    return;
                }
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleClientMessage(conn, json);
            }
        }

        private async Task HandleClientMessage(Connection conn, string json)
        {
            // 超軽量 JSON パース: op / topics / topic を小さく取り出すだけ
            // Minimal JSON parse: extract only op / topics / topic
            var op = ExtractJsonString(json, "\"op\"");
            if (op == "subscribe")
            {
                var topics = ExtractJsonStringArray(json, "\"topics\"");
                foreach (var t in topics)
                {
                    conn.Topics.Add(t);
                    if (_handlers.TryGetValue(t, out var handler))
                    {
                        var snap = await handler.GetSnapshotJsonAsync();
                        var env = $"{{\"op\":\"snapshot\",\"topic\":\"{EscapeJsonString(t)}\",\"data\":{snap}}}";
                        conn.EnqueueSend(env);
                    }
                }
            }
            else if (op == "unsubscribe")
            {
                var topics = ExtractJsonStringArray(json, "\"topics\"");
                foreach (var t in topics) conn.Topics.Remove(t);
            }
            else if (op == "snapshot")
            {
                var topic = ExtractJsonString(json, "\"topic\"");
                if (_handlers.TryGetValue(topic, out var handler))
                {
                    var snap = await handler.GetSnapshotJsonAsync();
                    var env = $"{{\"op\":\"snapshot\",\"topic\":\"{EscapeJsonString(topic)}\",\"data\":{snap}}}";
                    conn.EnqueueSend(env);
                }
            }
        }

        private async Task SendLoop(Connection conn, CancellationToken ct)
        {
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await conn.DequeueSendAsync(ct);
                if (msg == null) continue;
                var bytes = Encoding.UTF8.GetBytes(msg);
                await conn.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
        }

        private static string EscapeJsonString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // 最小 JSON ヘルパ: "key":"value" パターンから value を返す
        // Minimal JSON helper: extract string value after a "key": marker
        private static string ExtractJsonString(string json, string key)
        {
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return "";
            var colon = json.IndexOf(':', idx);
            if (colon < 0) return "";
            var q1 = json.IndexOf('"', colon);
            if (q1 < 0) return "";
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        // 最小 JSON ヘルパ: "key":[ "a", "b" ] から文字列配列を返す
        // Minimal JSON helper: extract string array after a "key": marker
        private static List<string> ExtractJsonStringArray(string json, string key)
        {
            var result = new List<string>();
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return result;
            var lb = json.IndexOf('[', idx);
            if (lb < 0) return result;
            var rb = json.IndexOf(']', lb);
            if (rb < 0) return result;
            var inside = json.Substring(lb + 1, rb - lb - 1);
            var pos = 0;
            while (pos < inside.Length)
            {
                var q1 = inside.IndexOf('"', pos);
                if (q1 < 0) break;
                var q2 = inside.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                result.Add(inside.Substring(q1 + 1, q2 - q1 - 1));
                pos = q2 + 1;
            }
            return result;
        }

        /// <summary>
        /// 1 本の WS 接続の状態
        /// State of a single WS connection
        /// </summary>
        private sealed class Connection
        {
            public Guid Id { get; }
            public WebSocket WebSocket { get; }
            public HashSet<string> Topics { get; } = new();
            private readonly System.Threading.Channels.Channel<string> _sendChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

            public Connection(Guid id, WebSocket webSocket)
            {
                Id = id;
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
