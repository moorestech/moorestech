using System.Collections.Generic;
using Server.Protocol;

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
            while (_frozen.Count != 0)
            {
                var entry = _frozen.Dequeue();
                if (!entry.IsActive) continue;

                // dirty問い合わせだけは同じ順序番号のまま次tickへ戻す
                // Return only dirty-network queries to the next tick with the same sequence
                if (entry.Process() == TickEndPacketProcessResult.Deferred)
                {
                    RestoreDeferredPackets(entry);
                    return;
                }
            }

            #region Internal

            void RestoreDeferredPackets(ITickEndPacketEntry current)
            {
                lock (_gate)
                {
                    var restored = new Queue<ITickEndPacketEntry>();
                    restored.Enqueue(current);
                    while (_frozen.Count != 0) restored.Enqueue(_frozen.Dequeue());
                    while (_receiving.Count != 0) restored.Enqueue(_receiving.Dequeue());
                    _receiving = restored;
                    _frozen = new Queue<ITickEndPacketEntry>();
                }
            }

            #endregion
        }
    }
}
