using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentQueue<List<byte>> _receiveQueue = new();

        public ReceiveQueueProcessor(PacketResponseCreator packetResponseCreator, SendQueueProcessor sendQueueProcessor)
        {
            _packetResponseCreator = packetResponseCreator;
            _sendQueueProcessor = sendQueueProcessor;

            // GameUpdaterのUpdate時にキューを処理
            _updateSubscription = GameUpdater.UpdateObservable.Subscribe(_ => ProcessReceiveQueue());
        }

        public void EnqueuePacket(List<byte> packet)
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
                    result.InsertRange(0, ToByteList.Convert(result.Count));
                    var array = result.ToArray();

                    // 送信キューに追加（実際の送信は送信スレッドで行う）
                    _sendQueueProcessor.EnqueueSendData(array);
                }
            }
        }

        public void Dispose()
        {
            _updateSubscription.Dispose();
        }
    }
}
