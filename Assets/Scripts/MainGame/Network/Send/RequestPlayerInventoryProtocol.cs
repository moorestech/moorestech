using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class RequestPlayerInventoryProtocol
    {
        private const short ProtocolId = 3;
        private readonly ISocket _socket;
        private readonly int playerId;

        public RequestPlayerInventoryProtocol(ISocket socket,PlayerConnectionSetting playerConnectionSetting)
        {
            playerId = playerConnectionSetting.PlayerId;
            _socket = socket;
        }
        
        public void Send()
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(playerId));
            
            _socket.Send(packet);
        }
    }
}