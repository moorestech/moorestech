using System;
using System.Collections.Generic;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class RequestEventProtocol : ITickable
    {
        private const int RequestEventIntervalMilliSeconds = 100;
        private const short ProtocolId = 4;

        private readonly int _playerId;
        private readonly ISocket _socket;

        public RequestEventProtocol(ISocket socket,PlayerConnectionSetting playerSettings)
        {
            _socket = socket;
            _playerId = playerSettings.PlayerId;
        }
        
        private DateTime _lastRequestTime = DateTime.Now;
        public void Tick()
        {
            if (DateTime.Now - _lastRequestTime < TimeSpan.FromMilliseconds(RequestEventIntervalMilliSeconds)) return;

            _lastRequestTime = DateTime.Now;
            Send(_playerId);
            
        }

        public void Send(int playerId)
        {
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(playerId));
            
            _socket.Send(packet.ToArray());
        }

    }
}