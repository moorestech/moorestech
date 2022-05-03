using System.Collections.Generic;
using MainGame.Network.Util;

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
            var packet = new List<byte>();
            
            //実際に送るデータの作成
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert((short)command.Length));
            packet.AddRange(ToByteList.Convert(command));
            
            _socket.Send(packet);
        }
    }
}