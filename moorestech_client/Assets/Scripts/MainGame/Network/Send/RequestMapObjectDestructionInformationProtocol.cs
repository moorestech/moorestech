using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;
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

        private void Send()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestMapObjectDestructionInformationMessagePack()).ToList());
        }

        public void Initialize() { }
    }
}