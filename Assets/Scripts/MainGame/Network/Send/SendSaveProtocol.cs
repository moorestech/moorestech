using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendSaveProtocol
    {
        
        private const short ProtocolId = 17;
        private readonly ISocket _socket;

        public SendSaveProtocol(ISocket socket)
        {
            _socket = socket;
        }

        public void Send()
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            
            _socket.Send(MessagePackSerializer.Serialize(new SaveProtocolMessagePack()).ToList());
        }
    }
}