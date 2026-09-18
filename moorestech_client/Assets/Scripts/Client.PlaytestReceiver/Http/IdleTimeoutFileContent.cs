using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 署名付きPUTの本文。固定長チャンクを出力へ書き終えるたびにアイドル期限を延ばし、本文を送り終えたら応答待ちの期限へ切り替える
    // The presigned PUT body; every fixed-size chunk written to the output extends the idle deadline, and once the body is out the deadline switches to the response wait
    // 読み出しではなく書き出しの完了を進捗とするのは、送り手のバッファに溜まっただけの読み出しや1回の遅いwriteで、進んでいる送信を切らないため
    // Progress is a completed write rather than a read, so bytes merely queued behind a buffered read or one slow write never cut a moving upload
    public sealed class IdleTimeoutFileContent : HttpContent
    {
        internal const int ChunkBytes = 64 * 1024;

        private readonly Stream _source;
        private readonly long _bytes;
        private readonly CancellationTokenSource _deadline;
        private readonly TimeSpan _idleTimeout;
        private readonly TimeSpan _responseTimeout;

        public IdleTimeoutFileContent(Stream source, long bytes, CancellationTokenSource deadline, TimeSpan idleTimeout, TimeSpan responseTimeout)
        {
            _source = source;
            _bytes = bytes;
            _deadline = deadline;
            _idleTimeout = idleTimeout;
            _responseTimeout = responseTimeout;
            Headers.ContentLength = bytes;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            // 接続確立から本文の書き始めまでも、進まなければアイドル期限で切る
            // From connecting until the body starts, a stall is also cut by the idle deadline
            _deadline.CancelAfter(_idleTimeout);
        }

        // 送信中に手元のファイルが読めなかった理由。HttpClientは本文の例外を包み直すため、到達失敗と分けられるようここに残す
        // Why the local file became unreadable mid-send; HttpClient rewraps body exceptions, so the reason is kept here to tell it apart from unreachability
        public string LocalReadFailure { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            // 送出はメインスレッドへ戻さない。非前面のEditorではメインスレッドの遅れがそのまま送信の遅れになる
            // Sending never hops back to the main thread; in a backgrounded Editor its lag would become the upload's lag
            // HTTPスタックは再送（リダイレクト・切れた接続の張り直し）で本文を2度書きうる。毎回先頭から読む
            // The HTTP stack may write the body twice on a resend (a redirect, a reopened connection); read from the start every time
            _source.Position = 0;
            var buffer = new byte[ChunkBytes];
            var remaining = _bytes;
            while (0 < remaining)
            {
                var read = await ReadChunkAsync(buffer, (int)Math.Min(buffer.Length, remaining)).ConfigureAwait(false);
                await stream.WriteAsync(buffer, 0, read, _deadline.Token).ConfigureAwait(false);
                _deadline.CancelAfter(_idleTimeout);
                remaining -= read;
            }
            // 本文を送り終えた。以降は受け手の処理と応答を待つ時間なので、アイドル期限ではなく応答待ちの期限で切る
            // The body is out; what remains is the receiver's processing and answer, so the response deadline applies instead of the idle one
            _deadline.CancelAfter(_responseTimeout);
        }

        // 手元のディスクI/O境界（他プロセスのロック・削除・縮小）。失敗理由を残して例外は送信側へそのまま流し、送信を止める
        // The local disk I/O boundary (a foreign lock, deletion or truncation); the reason is recorded and the exception flows on to abort the send
        private async Task<int> ReadChunkAsync(byte[] buffer, int count)
        {
            int read;
            try
            {
                read = await _source.ReadAsync(buffer, 0, count, _deadline.Token).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                LocalReadFailure = $"reading the file failed mid-send: {exception.Message}";
                throw;
            }
            if (read != 0) return read;

            // 宣言した長さより先にファイルが尽きた。Content-Lengthどおりに送れないので自分で止める
            // The file ended before the declared length; it cannot be sent as its Content-Length says, so stop here
            LocalReadFailure = $"the file ended before its declared {_bytes} bytes";
            throw new IOException(LocalReadFailure);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }
}
