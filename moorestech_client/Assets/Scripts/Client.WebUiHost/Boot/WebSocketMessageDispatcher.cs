using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// WS 受信メッセージの解釈と、snapshot/action 実行の境界隔離を担う
    /// Interprets received WS messages and owns the boundary isolation for snapshot/action execution
    /// </summary>
    internal sealed class WebSocketMessageDispatcher
    {
        private readonly ConcurrentDictionary<string, ITopicHandler> _handlers;
        private readonly ConcurrentDictionary<string, IActionHandler> _actionHandlers;

        public WebSocketMessageDispatcher(
            ConcurrentDictionary<string, ITopicHandler> handlers,
            ConcurrentDictionary<string, IActionHandler> actionHandlers)
        {
            _handlers = handlers;
            _actionHandlers = actionHandlers;
        }

        // 受信 JSON を解釈して op ごとに振り分ける。パース失敗は境界で握って接続を維持する
        // Parse the received JSON and route by op; parse failure is caught at the boundary to keep the connection alive
        public async UniTask DispatchAsync(WebSocketConnection conn, string json)
        {
            if (!WebUiJson.TryDeserialize<WsClientMessage>(json, out var msg) || msg.Op == null) return;

            switch (msg.Op)
            {
                case "subscribe":
                    if (msg.Topics == null) return;
                    foreach (var t in msg.Topics)
                    {
                        conn.Topics.TryAdd(t, 0);
                        await SendSnapshotAsync(conn, t);
                    }
                    break;
                case "unsubscribe":
                    if (msg.Topics == null) return;
                    foreach (var t in msg.Topics) conn.Topics.TryRemove(t, out _);
                    break;
                case "snapshot":
                    if (msg.Topic == null) return;
                    await SendSnapshotAsync(conn, msg.Topic);
                    break;
                case "action":
                    await HandleActionAsync(conn, msg);
                    break;
            }
        }

        // 指定接続へトピックの snapshot を送る
        // Send the topic snapshot to the given connection
        public async UniTask SendSnapshotAsync(WebSocketConnection conn, string topic)
        {
            if (!_handlers.TryGetValue(topic, out var handler)) return;
            var (ok, json) = await TryBuildSnapshotAsync(handler);
            if (!ok) return;
            conn.EnqueueSend(WebSocketEnvelope.BuildEnvelope("snapshot", topic, json));
        }

        // トピックを既に購読している全接続へ snapshot を再送する（Bind 前 subscribe 救済・2-E）
        // Re-send the snapshot to every connection already subscribed to the topic (rescues subscribe-before-Bind, 2-E)
        public async UniTask BroadcastSnapshotAsync(string topic, ICollection<WebSocketConnection> connections)
        {
            if (!_handlers.TryGetValue(topic, out var handler)) return;
            var targets = connections.Where(c => c.Topics.ContainsKey(topic)).ToList();
            if (targets.Count == 0) return;

            var (ok, json) = await TryBuildSnapshotAsync(handler);
            if (!ok) return;
            var envelope = WebSocketEnvelope.BuildEnvelope("snapshot", topic, json);
            foreach (var conn in targets) conn.EnqueueSend(envelope);
        }

        // action を実行して result を必ず返す。ハンドラ例外は境界で握り internal_error を返す（2-C）
        // Run the action and always return a result; handler exceptions are caught at the boundary as internal_error (2-C)
        private async UniTask HandleActionAsync(WebSocketConnection conn, WsClientMessage msg)
        {
            // requestId が無い action は応答相関できないため黙って捨てる
            // Drop actions without a requestId; the response cannot be correlated
            if (string.IsNullOrEmpty(msg.RequestId)) return;

            if (msg.Type == null || !_actionHandlers.TryGetValue(msg.Type, out var handler))
            {
                conn.EnqueueSend(WebSocketEnvelope.BuildResult(msg.RequestId, false, "unknown_action"));
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
                conn.EnqueueSend(WebSocketEnvelope.BuildResult(msg.RequestId, false, "host_stopping"));
                return;
            }

            var result = await ExecuteHandlerAsync();
            await UniTask.SwitchToTaskPool();
            conn.EnqueueSend(WebSocketEnvelope.BuildResult(msg.RequestId, result.Ok, result.Error));

            async UniTask<ActionResult> ExecuteHandlerAsync()
            {
                // ハンドラ実行の例外を境界で握り、接続維持のため必ず結果を返す（2-C）
                // Catch handler exceptions at the boundary and always return a result to keep the connection alive (2-C)
                try
                {
                    return await handler.ExecuteAsync(msg.Payload);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[WebSocketHub] action '{msg.Type}' failed: {e}");
                    return ActionResult.Fail("internal_error");
                }
            }
        }

        // snapshot 生成はメインスレッドでゲーム状態を読むため例外があり得る。境界で握って bool 化する（2-C）
        // Snapshot build reads game state on the main thread and can throw; catch at the boundary and turn into a bool (2-C)
        private async UniTask<(bool ok, string json)> TryBuildSnapshotAsync(ITopicHandler handler)
        {
            await UniTask.SwitchToMainThread();
            try
            {
                var json = await handler.GetSnapshotJsonAsync();
                await UniTask.SwitchToTaskPool();
                return (true, json);
            }
            catch (Exception e)
            {
                await UniTask.SwitchToTaskPool();
                UnityEngine.Debug.LogError($"[WebSocketHub] snapshot build failed: {e}");
                return (false, null);
            }
        }
    }
}
