using System.Threading;
using Core.Update;
using Game.SaveLoad.Snapshot;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop.PacketProcessing
{
    public class ReceiveQueueProcessor
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly SendQueueProcessor _sendQueueProcessor;
        private readonly PacketResponseContext _packetResponseContext;
        private readonly TickEndPacketQueue _tickEndPacketQueue;
        private readonly ReceivedPacketLog _receivedPacketLog;
        private int _isActive = 1;

        public ReceiveQueueProcessor(
            PacketResponseCreator packetResponseCreator,
            SendQueueProcessor sendQueueProcessor,
            PacketResponseContext packetResponseContext,
            TickEndPacketQueue tickEndPacketQueue,
            ReceivedPacketLog receivedPacketLog)
        {
            _packetResponseCreator = packetResponseCreator;
            _sendQueueProcessor = sendQueueProcessor;
            _packetResponseContext = packetResponseContext;
            _tickEndPacketQueue = tickEndPacketQueue;
            _receivedPacketLog = receivedPacketLog;
        }

        public void EnqueuePacket(byte[] packet)
        {
            // 受信スレッドでは世界を変更せず、全接続共通FIFOへ渡す
            // Keep world mutation off the receive thread and hand the packet to the shared FIFO
            if (Volatile.Read(ref _isActive) == 0)
            {
                Debug.LogWarning("切断済み接続からのパケット投入を拒否しました");
                return;
            }
            _tickEndPacketQueue.Enqueue(new ReceivedPacketEntry(this, packet));
        }

        public void Dispose()
        {
            // 切断確定後の新規投入だけを止め、先に積んだパケットは切断項目より前に処理する
            // Stop new enqueues after close; earlier packets still run before the disconnect entry
            Volatile.Write(ref _isActive, 0);
        }

        private void ProcessPacket(byte[] packet)
        {
            // 再生の真実はここ（tick末尾の処理点）。クライアント送信時刻ではなく処理tickで記録する
            // Replay truth lives here at the tick-end processing point; record the processing tick, not the client send time
            _receivedPacketLog.Append(GameUpdater.CurrentTick, _packetResponseContext.PlayerId, packet);

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

            public bool IsActive => true;

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
