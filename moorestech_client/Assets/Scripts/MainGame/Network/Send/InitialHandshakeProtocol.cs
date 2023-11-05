using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;
using VContainer.Unity;

namespace MainGame.Network.Send
{
    public class InitialHandshakeProtocol : IInitializable
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public InitialHandshakeProtocol(PlayerConnectionSetting playerConnectionSetting, ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;

            socketSender.OnConnected += OnConnected;
        }

        public void Initialize()
        {
        }

        private void OnConnected()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestInitialHandshakeMessagePack(
                _playerId, "Player " + _playerId)).ToList());
        }
    }
}