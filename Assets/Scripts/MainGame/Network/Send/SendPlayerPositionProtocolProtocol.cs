using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendPlayerPositionProtocolProtocol
    {
        private const short ProtocolId = 2;

        private readonly int _playerId;
        private readonly ISocket _socket;

        public SendPlayerPositionProtocolProtocol(ISocket socket,PlayerConnectionSetting playerConnectionSetting)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }
        public void Send(Vector2 pos)
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