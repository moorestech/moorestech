using System;
using System.Linq;
using Constant.Server;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class RequestEventProtocol : ITickable
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        private DateTime _lastRequestTime = DateTime.Now;

        public RequestEventProtocol(ISocketSender socketSender, PlayerConnectionSetting playerSettings)
        {
            _socketSender = socketSender;
            _playerId = playerSettings.PlayerId;
        }

        public void Tick()
        {
            if (DateTime.Now - _lastRequestTime < TimeSpan.FromMilliseconds(NetworkConst.UpdateIntervalMilliseconds)) return;

            _lastRequestTime = DateTime.Now;
            Send(_playerId);
        }

        private void Send(int playerId)
        {
            //TODO ポーリング化
            _socketSender.Send(MessagePackSerializer.Serialize(new EventProtocolMessagePack(playerId)).ToList());
        }
    }
}