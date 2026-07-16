using System.Threading;
using Server.Protocol;
using Server.Util;

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

        private TickEndPacketProcessResult ProcessPacket(byte[] packet)
        {
            var processResult = _packetResponseCreator.GetTickEndPacketResponse(
                packet, _packetResponseContext, out var results);
            if (processResult != TickEndPacketProcessResult.Completed) return processResult;

            foreach (var result in results)
            {
                // 長さヘッダーを付け、既存の送信専用スレッドへ引き渡す
                // Add the length header and hand data to the existing send-only thread
                var header = ToByteArray.Convert(result.Length);
                var sendData = new byte[header.Length + result.Length];
                header.CopyTo(sendData, 0);
                result.CopyTo(sendData, header.Length);
                _sendQueueProcessor.EnqueueSendData(sendData);
            }

            return TickEndPacketProcessResult.Completed;
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

            public TickEndPacketProcessResult Process()
            {
                return _owner.ProcessPacket(_packet);
            }
        }
    }
}
