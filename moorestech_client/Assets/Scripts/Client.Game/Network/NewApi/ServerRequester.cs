using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MainGame.Network;
using MessagePack;
using Server.Protocol;
using UniRx;

namespace Client.Network.NewApi
{
    public class ServerRequester
    {
        private readonly ISocketSender _socketSender;

        public ServerRequester(ISocketSender socketSender)
        {
            _socketSender = socketSender;
        }

        private readonly Dictionary<int, ResponseWaiter> _responseWaiters = new();

        public void ReceiveData(List<byte> data,int sequenceId)
        {
            if (!_responseWaiters.ContainsKey(sequenceId))
            {
                return;
            }
            _responseWaiters[sequenceId].WaitSubject.OnNext(data);
            _responseWaiters.Remove(sequenceId);
        }
        
        public void TimeOut(int sequenceId)
        {
            //TODO timeout処理
            if (!_responseWaiters.ContainsKey(sequenceId))
            {
                return;
            }
            _responseWaiters[sequenceId].WaitSubject.OnNext(null);
            _responseWaiters.Remove(sequenceId);
        }
        

        private int _sequenceId = 0;

        public async UniTask<TResponse> GetInformationData<TResponse>(ProtocolMessagePackBase sendData,CancellationToken ct) 
            where TResponse : ProtocolMessagePackBase
        {
            _sequenceId++;
            
            _socketSender.Send(MessagePackSerializer.Serialize(sendData).ToList());

            var awaiter = new Subject<List<byte>>();
            _responseWaiters.Add(_sequenceId,new ResponseWaiter(awaiter));

            var receiveData = await awaiter.ToUniTask(true, ct);
            
            if (receiveData == null)
            {
                return null;
            }

            return MessagePackSerializer.Deserialize<TResponse>(receiveData.ToArray());
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