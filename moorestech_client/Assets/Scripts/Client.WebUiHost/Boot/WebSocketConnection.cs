using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// 1 本の WS 接続の状態と送信ループ。送信は SemaphoreSlim で直列化し Close と競合させない
    /// State and send loop of a single WS connection; sends are serialized via SemaphoreSlim so they never race Close
    /// </summary>
    internal sealed class WebSocketConnection
    {
        public WebSocket WebSocket { get; }

        // 受信スレッドの subscribe と メインスレッドの Publish が同時に触るため並行辞書にする
        // Subscribed from the receive thread and read by main-thread Publish, so use a concurrent dict
        public ConcurrentDictionary<string, byte> Topics { get; } = new();

        private readonly Channel<string> _sendChannel = Channel.CreateUnbounded<string>();

        // action は受信ループから切り離して順番に捌く。120秒かかる書き出しの間もpingへ即答するため
        // Actions are handled off the receive loop in arrival order so a 120s write still answers pings immediately
        private readonly Channel<WsClientMessage> _actionChannel = Channel.CreateUnbounded<WsClientMessage>();

        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly CancellationTokenSource _stopCts = new();

        public WebSocketConnection(WebSocket webSocket)
        {
            WebSocket = webSocket;
        }

        public void EnqueueSend(string msg) => _sendChannel.Writer.TryWrite(msg);

        // 停止中の接続は受け取れない。捨てたことは呼び出し側がログへ出す
        // A stopping connection cannot take it; the caller logs what was dropped
        public bool EnqueueAction(WsClientMessage msg) => _actionChannel.Writer.TryWrite(msg);

        // 次の action を待つ。接続終了で待ちが解けたときは null を返す
        // Waits for the next action; returns null once the connection ended and the wait unblocked
        public async Task<WsClientMessage> ReadActionAsync(CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopCts.Token);
            if (!await _actionChannel.Reader.WaitToReadAsync(linked.Token)) return null;
            return _actionChannel.Reader.TryRead(out var msg) ? msg : null;
        }

        // 送信ループ。外部 ct か停止シグナルで終了する
        // Send loop; terminates on the external ct or the stop signal
        public async Task RunSendLoopAsync(CancellationToken externalCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCt, _stopCts.Token);
            var ct = linked.Token;
            while (WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await _sendChannel.Reader.ReadAsync(ct);
                await SendTextAsync(msg, ct);
            }
        }

        // 受信ループ終了後などに送信ループを止める
        // Stop the send loop, e.g. after the receive loop ends
        public void RequestStop()
        {
            _stopCts.Cancel();
            _sendChannel.Writer.TryComplete();
            _actionChannel.Writer.TryComplete();
        }

        // 送信ループを止め、送信ロックを取ってから単独で Close フレームを送る
        // Stop the send loop, take the send lock, then send the Close frame alone
        public async Task CloseAsync(CancellationToken ct)
        {
            _stopCts.Cancel();
            _sendChannel.Writer.TryComplete();
            _actionChannel.Writer.TryComplete();
            await _sendLock.WaitAsync(ct);
            try
            {
                if (WebSocket.State == WebSocketState.Open)
                {
                    await WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "server stopping", ct);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // 送信をロックで直列化し、CloseOutputAsync と SendAsync の同時実行を防ぐ
        // Serialize sends with the lock so SendAsync never overlaps CloseOutputAsync
        private async Task SendTextAsync(string msg, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _sendLock.WaitAsync(ct);
            try
            {
                if (WebSocket.State == WebSocketState.Open)
                {
                    await WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
