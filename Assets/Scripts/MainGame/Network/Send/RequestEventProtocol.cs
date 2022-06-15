using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class RequestEventProtocol : ITickable
    {
        private const int RequestEventIntervalMilliSeconds = 100;

        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public RequestEventProtocol(ISocketSender socketSender,PlayerConnectionSetting playerSettings)
        {
            _socketSender = socketSender;
            _playerId = playerSettings.PlayerId;
        }
        
        private DateTime _lastRequestTime = DateTime.Now;
        public void Tick()
        {
            if (DateTime.Now - _lastRequestTime < TimeSpan.FromMilliseconds(RequestEventIntervalMilliSeconds)) return;

            _lastRequestTime = DateTime.Now;
            Send(_playerId);
            
        }

        private void Send(int playerId)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new EventProtocolMessagePack(playerId)).ToList());
        }

    }
}