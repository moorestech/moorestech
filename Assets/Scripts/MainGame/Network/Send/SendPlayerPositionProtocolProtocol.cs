using System.Collections.Generic;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendPlayerPositionProtocolProtocol
    {
        private const short ProtocolId = 2;
        private readonly ISocket _socket;

        public SendPlayerPositionProtocolProtocol(ISocket socket)
        {
            _socket = socket;
        }
        public void Send(int playerId, Vector2 position)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(position.x));
            packet.AddRange(ToByteList.Convert(position.y));
            packet.AddRange(ToByteList.Convert(playerId));
            
            _socket.Send(packet.ToArray());
        }
    }
}