using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Tests.WebUiHost.Transport
{
    internal sealed class CloseFailureSocket : WebSocket
    {
        private readonly bool _fail;
        private readonly bool _failAbort;
        private readonly bool _failDispose;
        public int DisposeAttempts { get; private set; }
        private WebSocketState _state = WebSocketState.Open;
        public int CloseAttempts { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string CloseStatusDescription => null;
        public override string SubProtocol => null;
        public override WebSocketState State => _state;

        public CloseFailureSocket(bool fail, bool failAbort, bool failDispose)
        {
            _fail = fail;
            _failAbort = failAbort;
            _failDispose = failDispose;
        }
        public override void Abort()
        {
            if (_failAbort) throw new WebSocketException("injected abort failure");
            _state = WebSocketState.Aborted;
        }
        public override void Dispose()
        {
            DisposeAttempts++;
            if (_failDispose) throw new WebSocketException("injected dispose failure");
            if (_state != WebSocketState.Aborted) _state = WebSocketState.Closed;
        }
        public override Task CloseAsync(WebSocketCloseStatus status, string reason, CancellationToken token) => CloseOutputAsync(status, reason, token);
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string reason, CancellationToken token)
        {
            // ネットワーク送信境界だけを故障注入し、実Connectionの後始末を通す
            // Inject failure only at network output while executing real connection cleanup
            CloseAttempts++;
            if (!_fail) _state = WebSocketState.CloseSent;
            return _fail ? Task.FromException(new WebSocketException("injected close output failure")) : Task.CompletedTask;
        }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token) => throw new NotSupportedException();
    }
}
