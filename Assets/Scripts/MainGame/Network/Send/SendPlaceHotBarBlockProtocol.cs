using System.Collections.Generic;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    public class SendPlaceHotBarBlockProtocol
    {
        private const short ProtocolId = 8;
        
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendPlaceHotBarBlockProtocol(ISocket socket,PlayerConnectionSetting playerConnectionSetting)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y, short hotBarSlot)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(hotBarSlot));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(_playerId));

            _socket.Send(packet.ToArray());
        }
    }
}