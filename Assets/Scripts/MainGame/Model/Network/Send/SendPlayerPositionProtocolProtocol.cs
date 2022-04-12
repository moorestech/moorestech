using System;
using System.Collections.Generic;
using MainGame.Network;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using VContainer.Unity;

namespace MainGame.Model.Network.Send
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
        public void Send(float x,float y)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(_playerId));
            
            _socket.Send(packet.ToArray());
        }
    }
}