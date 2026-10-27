using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Client.WebUiHost.Boot.Transport
{
    internal static class WebSocketConnectionCloser
    {
        internal static async Task<WebSocketCloseResult> CloseAllAsync(ICollection<WebSocketConnection> connections)
        {
            // 最大 2 秒タイムアウト付きで Close フレームを送信
            // Send Close frames with a 2-second timeout to avoid blocking forever
            var unterminated = new Dictionary<Guid, string>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            foreach (var conn in connections)
            {
                // WebSocket Closeフレームのネットワーク送信失敗を接続ごとに隔離する
                // Isolate WebSocket Close frame network transmission failures per connection
                try
                {
                    await conn.CloseAsync(cts.Token);
                }
                catch (Exception e)
                {
                    var reason = e.GetBaseException().Message;
                    LogFailure(conn.Id, "close failed: " + reason);
                    // Close送信が失敗した本人Socketを強制切断する
                    // Abort only this socket after its Close frame network send fails
                    try { conn.WebSocket.Abort(); }
                    catch (Exception abortError) { LogFailure(conn.Id, "abort failed: " + abortError.Message); }
                }
                // ネットワーク資源解放も隔離し、CloseSentを終了済みと誤認しない
                // Isolate network resource disposal and never mistake CloseSent for termination
                try { conn.WebSocket.Dispose(); }
                catch (Exception disposeError) { LogFailure(conn.Id, "dispose failed: " + disposeError.Message); }
                var state = conn.WebSocket.State;
                if (state != WebSocketState.Closed && state != WebSocketState.Aborted)
                {
                    var reason = $"Socket remains {state} after close/abort/dispose";
                    unterminated.Add(conn.Id, reason);
                    UnityEngine.Debug.LogError($"[WebSocketHub] unterminated {conn.Id}: {reason}");
                }
            }
            return new WebSocketCloseResult(unterminated);
        }

        private static void LogFailure(Guid id, string reason)
        {
            UnityEngine.Debug.LogError($"[WebSocketHub] {reason} for {id}");
        }
    }
}
