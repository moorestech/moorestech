using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MessagePack;
using Server.Protocol;
using UniRx;
using UnityEngine;

namespace Client.Network.API
{
    /// <summary>
    ///     送信されたパケットの応答パケットを<see cref="ServerCommunicator" />から受け取り、呼び出し元に返すクラス
    /// </summary>
    public class PacketExchangeManager
    {
        private readonly PacketSender _packetSender;
        
        private readonly Dictionary<int, ResponseWaiter> _responseWaiters = new();
        
        private int _sequenceId;
        
        public PacketExchangeManager(PacketSender packetSender)
        {
            _packetSender = packetSender;
            TimeOutUpdate().Forget();
        }
        
        private async UniTask TimeOutUpdate()
        {
            while (true)
            {
                for (var i = _responseWaiters.Count - 1; i >= 0; i--)
                {
                    var sequenceId = _responseWaiters.Keys.ElementAt(i);
                    var waiter = _responseWaiters[sequenceId];
                    var time = DateTime.Now - waiter.SendTime;
                    if (time.TotalSeconds < 10) continue;
                    
                    _responseWaiters[sequenceId].WaitSubject.OnNext(null);
                    _responseWaiters.Remove(sequenceId);
                }
                
                await UniTask.Delay(1000);
            }
        }
        
        public async UniTask ExchangeReceivedPacket(List<byte> data)
        {
            var response = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(data.ToArray());
            var sequence = response.SequenceId;
            
            await UniTask.SwitchToMainThread();
            
            if (!_responseWaiters.ContainsKey(sequence)) return;
            _responseWaiters[sequence].WaitSubject.OnNext(data);
            _responseWaiters.Remove(sequence);
        }
        
        [CanBeNull]
        public async UniTask<TResponse> GetPacketResponse<TResponse>(ProtocolMessagePackBase request, CancellationToken ct) where TResponse : ProtocolMessagePackBase
        {
            SendPacket();
            
            return await WaitReceive();
            
            #region Internal
            
            void SendPacket()
            {
                _sequenceId++;
                request.SequenceId = _sequenceId;
                _packetSender.Send(request);
            }
            
            async UniTask<TResponse> WaitReceive()
            {
                var responseWaiter = new ResponseWaiter(new Subject<List<byte>>());
                _responseWaiters.Add(_sequenceId, responseWaiter);
                
                var receiveData = await responseWaiter.WaitSubject.ToUniTask(true, ct);
                if (receiveData == null)
                {
                    Debug.Log("Receive null");
                    return null;
                }
                
                try
                {
                    return MessagePackSerializer.Deserialize<TResponse>(receiveData.ToArray());
                }
                catch (Exception e)
                {
                    Debug.LogError($"Deserialization failed. Tag:{request.Tag}\n{e.Message}\n{e.StackTrace}");
                    Console.WriteLine(e);
                    return null;
                }
            }
            
            #endregion
        }
    }
    
    
    public class ResponseWaiter
    {
        public ResponseWaiter(Subject<List<byte>> waitSubject)
        {
            WaitSubject = waitSubject;
            SendTime = DateTime.Now;
        }
        
        public Subject<List<byte>> WaitSubject { get; }
        public DateTime SendTime { get; }
    }
}