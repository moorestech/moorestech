using System;
using System.Collections.Concurrent;
using Core.Update;
using Server.Protocol;
using Server.Util;
using UniRx;

namespace Server.Boot.Loop.PacketProcessing
{
    /// <summary>
    /// 受信キュープロセッサ
    /// 受信スレッドからパケットをEnqueueし、メインスレッドでGetPacketResponseを実行
    /// 処理結果をSendQueueProcessorに送信
    /// ConcurrentQueueを使用してlock-freeで高速処理
    /// </summary>
    public class ReceiveQueueProcessor
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly SendQueueProcessor _sendQueueProcessor;
        private readonly IDisposable _updateSubscription;
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new();

        public ReceiveQueueProcessor(PacketResponseCreator packetResponseCreator, SendQueueProcessor sendQueueProcessor)
        {
            _packetResponseCreator = packetResponseCreator;
            _sendQueueProcessor = sendQueueProcessor;

            // GameUpdaterのUpdate時にキューを処理
            _updateSubscription = GameUpdater.LateUpdateObservable.Subscribe(_ => ProcessReceiveQueue());
        }

        public void EnqueuePacket(byte[] packet)
        {
            _receiveQueue.Enqueue(packet);
        }

        private void ProcessReceiveQueue()
        {
            // 受信キューからパケットを取り出してゲームロジックで処理
            // この処理は常に一瞬で終わる（送信はSendQueueProcessorに委譲）
            while (_receiveQueue.TryDequeue(out var packet))
            {
                var results = _packetResponseCreator.GetPacketResponse(packet);
                foreach (var result in results)
                {
                    // パケット長ヘッダーを付与して送信データを構築
                    // Build send data with packet length header
                    var header = ToByteArray.Convert(result.Length);
                    var sendData = new byte[header.Length + result.Length];
                    header.CopyTo(sendData, 0);
                    result.CopyTo(sendData, header.Length);

                    // 送信キューに追加（実際の送信は送信スレッドで行う）
                    _sendQueueProcessor.EnqueueSendData(sendData);
                }
            }
        }

        public void Dispose()
        {
            _updateSubscription.Dispose();
        }
    }
}
