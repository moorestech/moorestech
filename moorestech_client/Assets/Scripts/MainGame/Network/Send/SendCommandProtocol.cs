using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendCommandProtocol
    {
        private readonly ISocketSender _socketSender;

        public SendCommandProtocol(ISocketSender socketSender)
        {
            _socketSender = socketSender;
        }

        public void SendCommand(string command)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new SendCommandProtocolMessagePack(
                command)).ToList());
        }
    }
}