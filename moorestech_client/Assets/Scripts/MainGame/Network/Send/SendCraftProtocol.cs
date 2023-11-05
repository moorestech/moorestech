using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendCraftProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public SendCraftProtocol(ISocketSender socketSender, PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socketSender = socketSender;
        }

        public void SendOneCraft()
        {
            //craft type id is 0
            _socketSender.Send(MessagePackSerializer.Serialize(new CraftProtocolMessagePack(
                _playerId, 0)).ToList());
        }

        public void SendAllCraft()
        {
            //craft type id is 1
            _socketSender.Send(MessagePackSerializer.Serialize(new CraftProtocolMessagePack(
                _playerId, 1)).ToList());
        }

        public void SendOneStackCraft()
        {
            //craft type id is 2
            _socketSender.Send(MessagePackSerializer.Serialize(new CraftProtocolMessagePack(
                _playerId, 2)).ToList());
        }
    }
}