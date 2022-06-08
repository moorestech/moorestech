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
        private ISocket _socket;

        public SendCommandProtocol(ISocket socket)
        {
            _socket = socket;
        }
        
        public void SendCommand(string command)
        {
            _socket.Send(MessagePackSerializer.Serialize(new SendCommandProtocolMessagePack(
                command)).ToList());
        }
    }
}