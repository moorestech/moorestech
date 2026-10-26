using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Client.WebUiHost.Boot.Transport
{
    internal static class WebSocketConnectionCloser
    {
        internal static async Task<WebSocketCloseResult> CloseAllAsync(ICollection<WebSocketConnection> connections)
        {
            // 最大 2 秒タイムアウト付きで Close フレームを送信
            // Send Close frames with a 2-second timeout to avoid blocking forever
            var failures = new Dictionary<Guid, string>();
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
                    failures.Add(conn.Id, reason);
                    UnityEngine.Debug.LogError($"[WebSocketHub] close failed for {conn.Id}: {reason}");
                }
            }
            return new WebSocketCloseResult(failures);
        }

    }
}
