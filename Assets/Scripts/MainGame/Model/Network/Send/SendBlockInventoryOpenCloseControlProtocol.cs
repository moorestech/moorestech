using System.Collections.Generic;
using MainGame.Network;
using MainGame.Network.Settings;
using MainGame.Network.Util;

namespace MainGame.Model.Network.Send
{
    public class SendBlockInventoryOpenCloseControlProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 16;
        private readonly int _playerId;

        
        public SendBlockInventoryOpenCloseControlProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y,bool isOpen)
        {
            
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(_playerId));
            packet.Add(isOpen ? (byte)1 : (byte)0);

            _socket.Send(packet.ToArray());
        }
    }
}