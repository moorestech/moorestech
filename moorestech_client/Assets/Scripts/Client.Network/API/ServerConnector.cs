using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Network;
using MessagePack;
using Server.Protocol;
using UniRx;

namespace Client.Network.API
{
    /// <summary>
    /// TODO リネーム
    /// </summary>
    public class ServerConnector
    {
        private readonly ISocketSender _socketSender;

        public ServerConnector(ISocketSender socketSender)
        {
            _socketSender = socketSender;
            TimeOutUpdate().Forget();
        }

        private readonly Dictionary<int, ResponseWaiter> _responseWaiters = new();
        
        
        private async UniTask TimeOutUpdate()
        {
            while (true)
            {
                for (var i = _responseWaiters.Count - 1; i >= 0; i--)
                {
                    var sequenceId = _responseWaiters.Keys.ElementAt(i);
                    var waiter = _responseWaiters[sequenceId];
                    var time = DateTime.Now - waiter.SendTime;
                    if (time.TotalSeconds < 10)
                    {
                        continue;
                    }

                    _responseWaiters[sequenceId].WaitSubject.OnNext(null);
                    _responseWaiters.Remove(sequenceId);
                }

                await UniTask.Delay(1000);
            }
        }

        public async UniTask ReceiveData(List<byte> data)
        {
            var response = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(data.ToArray());
            var sequence = response.SequenceId;
            
            await UniTask.SwitchToMainThread();
                
            if (!_responseWaiters.ContainsKey(sequence))
            {
                return;
            }
            _responseWaiters[sequence].WaitSubject.OnNext(data);
            _responseWaiters.Remove(sequence);
        }

        private int _sequenceId = 0;

        public void Send(ProtocolMessagePackBase sendData)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(sendData).ToList());
        } 

        public async UniTask<TResponse> GetInformationData<TResponse>(ProtocolMessagePackBase sendData,CancellationToken ct) where TResponse : ProtocolMessagePackBase
        {
            SendPacket();
            
            return await WaitReceive();

            #region Internal

            void SendPacket()
            {
                _socketSender.Send(MessagePackSerializer.Serialize(sendData).ToList());
                _sequenceId++;
            }
            
            async UniTask<TResponse> WaitReceive()
            {
                var responseWaiter = new ResponseWaiter(new Subject<List<byte>>());
                _responseWaiters.Add(_sequenceId,responseWaiter);

                var receiveData = await responseWaiter.WaitSubject.ToUniTask(true, ct);
                if (receiveData == null)
                {
                    return null;
                }

                return MessagePackSerializer.Deserialize<TResponse>(receiveData.ToArray());
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

        public Subject<List<byte>> WaitSubject { get; private set; }
        public DateTime SendTime { get; private set; }
        
    }
}