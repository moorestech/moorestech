using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendCommandProtocol
    {
        private const short ProtocolId = 11;
        private ISocketSender _socketSender;

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