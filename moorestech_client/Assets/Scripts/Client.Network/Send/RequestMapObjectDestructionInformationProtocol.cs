using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Protocol.PacketResponse;
using UniRx;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class RequestMapObjectDestructionInformationProtocol : IInitializable
    {
        private readonly ISocketSender _socketSender;

        public RequestMapObjectDestructionInformationProtocol(ISocketSender socketSender)
        {
            _socketSender = socketSender;
            //接続した時の初回送信
            _socketSender.OnConnected += Send;
        }

        public void Initialize()
        {
        }

        private void Send()
        {
        }

        private readonly Subject<byte[]> _onReceiveData = new();

        public void ReceiveData(byte[] data)
        {
            _onReceiveData.OnNext(data);
        }
        
        public void TimeOut()
        {
            _onReceiveData.OnError(new TimeoutException());
        }
        
        

        public async UniTask<List<MapObjectDestructionInformationData>> GetInformationData()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestMapObjectDestructionInformationMessagePack()).ToList());

            var receiveData = await _onReceiveData.ToUniTask(true);
        }
    }
}