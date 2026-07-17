using System.Collections.Generic;

namespace Server.Boot.Loop.PacketProcessing
{
    public class TickEndPacketQueue
    {
        private readonly object _gate = new();
        private Queue<ITickEndPacketEntry> _receiving = new();
        private Queue<ITickEndPacketEntry> _frozen = new();

        public void Enqueue(ITickEndPacketEntry entry)
        {
            // 格納を一つのロックで直列化し、接続を跨いだ到着順を確定する
            // Serialize enqueue under one lock to establish arrival order across connections
            lock (_gate)
            {
                _receiving.Enqueue(entry);
            }
        }

        public void FreezeCurrentPackets()
        {
            // tick末尾開始時のキューを交換し、以後の受信を次tickへ分離する
            // Swap the queue at tick-end start so later arrivals belong to the next tick
            lock (_gate)
            {
                _frozen = _receiving;
                _receiving = new Queue<ITickEndPacketEntry>();
            }
        }

        public void ProcessFrozenPackets()
        {
            // 問い合わせは今tickの確定済み状態で即答する（設置tickと網反映tickのズレは仕様）
            // Queries answer immediately from this tick's settled state; the placement-vs-network tick gap is by design
            while (_frozen.Count != 0)
            {
                var entry = _frozen.Dequeue();
                if (!entry.IsActive) continue;
                entry.Process();
            }
        }
    }
}
