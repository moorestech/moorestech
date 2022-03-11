using System;
using System.Collections.Generic;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class SendPlayerPositionProtocolProtocol : ITickable
    {
        private const int RequestEventIntervalMilliSeconds = 100;
        
        private const short ProtocolId = 2;

        private readonly int _playerId;
        private readonly ISocket _socket;
        private readonly IPlayerPosition _playerPosition;

        public SendPlayerPositionProtocolProtocol(ISocket socket,IPlayerPosition playerPosition,PlayerConnectionSetting playerConnectionSetting)
        {
            _socket = socket;
            _playerPosition = playerPosition;
            _playerId = playerConnectionSetting.PlayerId;
        }
        
        private DateTime _lastRequestTime = DateTime.Now;
        public void Tick()
        {
            if (DateTime.Now - _lastRequestTime < TimeSpan.FromMilliseconds(RequestEventIntervalMilliSeconds)) return;
            
            _lastRequestTime = DateTime.Now;
            Send();
        }
        public void Send()
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(_playerPosition.GetPlayerPosition().x));
            packet.AddRange(ToByteList.Convert(_playerPosition.GetPlayerPosition().y));
            packet.AddRange(ToByteList.Convert(_playerId));
            
            _socket.Send(packet.ToArray());
        }
    }
}