using System.Collections.Concurrent;

namespace Client.Game.InGame.BugReport.Recording
{
    // 録画フレームのバッファを使い回す固定数のプール。1枚3.6MBを毎フレーム新規確保するとGCを焚き続ける
    // A fixed pool of reused frame buffers; allocating 3.6MB per frame would keep the GC burning
    public sealed class FrameBufferPool
    {
        // 借り手はメインスレッド、返し手はffmpegの書き込みスレッドなのでスレッド安全な器で持つ
        // The borrower is the main thread and the returner is ffmpeg's writer thread, so the container is thread-safe
        private readonly ConcurrentQueue<byte[]> _free = new();

        public FrameBufferPool(int bufferCount, int bufferLength)
        {
            for (var i = 0; i < bufferCount; i++) _free.Enqueue(new byte[bufferLength]);
        }

        public bool TryRent(out byte[] buffer)
        {
            return _free.TryDequeue(out buffer);
        }

        public void Return(byte[] buffer)
        {
            _free.Enqueue(buffer);
        }
    }
}
