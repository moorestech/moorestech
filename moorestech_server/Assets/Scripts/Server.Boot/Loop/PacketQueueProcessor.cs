using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using Core.Update;
using Server.Protocol;
using Server.Util;
using UniRx;

namespace Server.Boot.Loop
{
    public class PacketQueueProcessor
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly Socket _client;
        private readonly IDisposable _updateSubscription;
        
        private readonly ConcurrentQueue<List<byte>> _packetQueue = new();

        public PacketQueueProcessor(Socket client, PacketResponseCreator packetResponseCreator)
        {
            // GameUpdaterのUpdate時にキューを処理
            _updateSubscription = GameUpdater.UpdateObservable.Subscribe(_ => ProcessQueue());
            _client = client;
            _packetResponseCreator = packetResponseCreator;
        }

        public void EnqueuePacket(List<byte> request)
        {
            _packetQueue.Enqueue(request);
        }

        private void ProcessQueue()
        {
            // キュー内のすべてのパケットを処理（到着順序を保証）
            while (_packetQueue.TryDequeue(out var request))
            {
                var results = _packetResponseCreator.GetPacketResponse(request);
                foreach (var result in results)
                {
                    result.InsertRange(0, ToByteList.Convert(result.Count));
                    var array = result.ToArray();
                    _client.Send(array);
                }
            }
        }
        
        public void Dispose()
        {
            _updateSubscription.Dispose();
        }
    }
}
