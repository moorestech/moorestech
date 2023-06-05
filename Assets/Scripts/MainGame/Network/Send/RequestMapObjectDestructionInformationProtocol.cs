using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class RequestMapObjectDestructionInformationProtocol
    {
        private readonly ISocketSender _socketSender;

        public RequestMapObjectDestructionInformationProtocol(ISocketSender socketSender)
        {
            _socketSender = socketSender;
            //接続した時の初回送信
            _socketSender.OnConnected += Send;
        }
        
        public void Send()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestMapObjectDestructionInformationMessagePack()).ToList());
        }

    }
}