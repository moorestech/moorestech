using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 読み出しが進むたびにアイドル期限を延ばす読み取り専用ストリーム。送信本文に噛ませて「進んでいる限り切らない」を実現する
    // A read-only stream that extends the idle deadline on every read; wrapped around the request body so a moving upload is never cut
    public sealed class IdleTimeoutStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationTokenSource _idle;
        private readonly TimeSpan _idleTimeout;

        public IdleTimeoutStream(Stream inner, CancellationTokenSource idle, TimeSpan idleTimeout)
        {
            _inner = inner;
            _idle = idle;
            _idleTimeout = idleTimeout;
            _idle.CancelAfter(_idleTimeout);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (0 < read) _idle.CancelAfter(_idleTimeout);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            if (0 < read) _idle.CancelAfter(_idleTimeout);
            return read;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
