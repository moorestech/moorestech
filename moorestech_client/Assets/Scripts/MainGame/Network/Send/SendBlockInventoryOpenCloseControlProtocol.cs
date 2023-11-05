using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendBlockInventoryOpenCloseControlProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;


        public SendBlockInventoryOpenCloseControlProtocol(PlayerConnectionSetting playerConnectionSetting, ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y, bool isOpen)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new BlockInventoryOpenCloseProtocolMessagePack(
                _playerId, x, y, isOpen)).ToList());
        }
    }
}