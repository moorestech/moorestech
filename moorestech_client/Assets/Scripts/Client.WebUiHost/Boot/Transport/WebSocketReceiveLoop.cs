using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.WebUiHost.Boot.Transport
{
    /// <summary>
    /// 受信フレームを組み立て、完成したJSONを既存dispatcherへ渡す
    /// Reassembles received frames and passes complete JSON messages to the existing dispatcher
    /// </summary>
    internal sealed class WebSocketReceiveLoop
    {
        private readonly WebSocketMessageDispatcher _dispatcher;

        internal WebSocketReceiveLoop(WebSocketMessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        internal async Task RunAsync(WebSocketConnection conn, CancellationToken ct)
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
