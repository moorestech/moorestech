using System.Threading;
using Server.Protocol;

namespace Server.Boot.Loop.PacketProcessing
{
    public class ReceiveQueueProcessor
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly SendQueueProcessor _sendQueueProcessor;
        private readonly PacketResponseContext _packetResponseContext;
        private readonly TickEndPacketQueue _tickEndPacketQueue;
        private int _isActive = 1;

        public ReceiveQueueProcessor(
            PacketResponseCreator packetResponseCreator,
            SendQueueProcessor sendQueueProcessor,
            PacketResponseContext packetResponseContext,
            TickEndPacketQueue tickEndPacketQueue)
        {
            _packetResponseCreator = packetResponseCreator;
            _sendQueueProcessor = sendQueueProcessor;
            _packetResponseContext = packetResponseContext;
            _tickEndPacketQueue = tickEndPacketQueue;
        }

        public void EnqueuePacket(byte[] packet)
        {
            // 受信スレッドでは世界を変更せず、全接続共通FIFOへ渡す
            // Keep world mutation off the receive thread and hand the packet to the shared FIFO
            _tickEndPacketQueue.Enqueue(new ReceivedPacketEntry(this, packet));
        }

        public void Dispose()
        {
            // 固定済みキューに残る項目も実行されないよう接続状態だけを落とす
            // Mark only connection state so already-frozen entries are skipped
            Volatile.Write(ref _isActive, 0);
        }

        private void ProcessPacket(byte[] packet)
        {
            var results = _packetResponseCreator.GetPacketResponse(packet, _packetResponseContext);

            foreach (var result in results)
            {
                // 送信キューに追加（長さヘッダ付与と実送信はSendQueueProcessorが行う）
                // Enqueue for send; SendQueueProcessor handles length framing and the actual send
                _sendQueueProcessor.EnqueueMessage(result);
            }
        }

        private sealed class ReceivedPacketEntry : ITickEndPacketEntry
        {
            private readonly ReceiveQueueProcessor _owner;
            private readonly byte[] _packet;

            public bool IsActive => Volatile.Read(ref _owner._isActive) != 0;

            public ReceivedPacketEntry(ReceiveQueueProcessor owner, byte[] packet)
            {
                _owner = owner;
                _packet = packet;
            }

            public void Process()
            {
                _owner.ProcessPacket(_packet);
            }
        }
    }
}
