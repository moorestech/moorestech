using System;
using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class InitialHandshakeProtocol : IInitializable
    {
        private readonly ISocketSender _socketSender;
        private readonly int _playerId;

        public InitialHandshakeProtocol(PlayerConnectionSetting playerConnectionSetting,ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
            
            socketSender.OnConnected += OnConnected;
        }

        private void OnConnected()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestInitialHandshakeMessagePack(
                _playerId,"Player " + _playerId)).ToList());
        }

        public void Initialize() { }
    }
}