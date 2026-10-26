using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Tests.WebUiHost.Transport
{
    internal sealed class CloseFailureSocket : WebSocket
    {
        private readonly bool _fail;
        public int CloseAttempts { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string CloseStatusDescription => null;
        public override string SubProtocol => null;
        public override WebSocketState State => WebSocketState.Open;

        public CloseFailureSocket(bool fail) { _fail = fail; }
        public override void Abort() { }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string reason, CancellationToken token) => CloseOutputAsync(status, reason, token);
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string reason, CancellationToken token)
        {
            // ネットワーク送信境界だけを故障注入し、実Connectionの後始末を通す
            // Inject failure only at network output while executing real connection cleanup
            CloseAttempts++;
            return _fail ? Task.FromException(new WebSocketException("injected close output failure")) : Task.CompletedTask;
        }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token) => throw new NotSupportedException();
    }
}
