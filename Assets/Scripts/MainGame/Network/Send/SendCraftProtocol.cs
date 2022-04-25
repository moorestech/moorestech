using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class SendCraftProtocol
    {
        private const short ProtocolId = 14;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void Send()
        {
            var packet = new List<byte>();
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(_playerId));
            
            _socket.Send(packet.ToArray());
            
        }
    }
}