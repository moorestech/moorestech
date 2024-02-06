using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendOneClickCraftProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public SendOneClickCraftProtocol(PlayerConnectionSetting playerConnectionSetting, ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int craftRecipeId)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(_playerId, craftRecipeId)).ToList());
        }
    }
}