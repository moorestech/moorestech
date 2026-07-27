using System.Collections.Concurrent;

namespace Server.Boot.Loop.PacketProcessing
{
    public class TickEndPacketQueue
    {
        private readonly ConcurrentQueue<ITickEndPacketEntry> _queue = new();
        private int _frozenCount;

        public void Enqueue(ITickEndPacketEntry entry)
        {
            // 受信スレッドからlock-freeで格納する。接続を跨いだ到着順はConcurrentQueueのFIFOが確定する
            // Lock-free enqueue from receive threads; ConcurrentQueue's FIFO fixes arrival order across connections
            _queue.Enqueue(entry);
        }

        public void FreezeCurrentPackets()
        {
            // tick末尾開始時の件数を固定し、以後の到着を次tickへ分離する
            // Snapshot the count at tick-end start so later arrivals belong to the next tick
            _frozenCount = _queue.Count;
        }

        public void ProcessFrozenPackets()
        {
            // 問い合わせは今tickの確定済み状態で即答する（設置tickと網反映tickのズレは仕様）
            // Queries answer immediately from this tick's settled state; the placement-vs-network tick gap is by design
            for (var i = 0; i < _frozenCount; i++)
            {
                if (!_queue.TryDequeue(out var entry)) break;
                if (!entry.IsActive) continue;
                entry.Process();
            }
            _frozenCount = 0;
        }
    }
}
