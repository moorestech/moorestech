using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class RequestPlayerInventoryProtocol
    {
        private readonly ISocketSender _socketSender;
        private readonly int playerId;

        public RequestPlayerInventoryProtocol(ISocketSender socketSender, PlayerConnectionSetting playerConnectionSetting)
        {
            playerId = playerConnectionSetting.PlayerId;
            _socketSender = socketSender;
            //接続した時の初回送信
            _socketSender.OnConnected += Send;
        }

        public void Send()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(playerId)).ToList());
        }
    }
}