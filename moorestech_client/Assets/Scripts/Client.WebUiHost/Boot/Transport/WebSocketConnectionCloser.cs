using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Client.WebUiHost.Boot.Transport
{
    internal static class WebSocketConnectionCloser
    {
        internal static async Task CloseAllAsync(ICollection<WebSocketConnection> connections)
        {
            // 最大 2 秒タイムアウト付きで Close フレームを送信
            // Send Close frames with a 2-second timeout to avoid blocking forever
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            foreach (var conn in connections)
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
        }

    }
}
