using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendMiningProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 15;
        private readonly int _playerId;
        
        public SendMiningProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }
        
        public void Send(Vector2Int pos)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(pos.x));
            packet.AddRange(ToByteList.Convert(pos.y));
            packet.AddRange(ToByteList.Convert(_playerId));
            
            _socket.Send(packet);
        }
    }
}