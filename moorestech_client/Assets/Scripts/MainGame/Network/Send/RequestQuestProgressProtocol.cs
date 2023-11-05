using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class RequestQuestProgressProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public RequestQuestProgressProtocol(ISocketSender socketSender, PlayerConnectionSetting playerConnectionSetting)
        {
            _playerId = playerConnectionSetting.PlayerId;
            _socketSender = socketSender;
            //接続した時の初回送信
            _socketSender.OnConnected += Send;
        }

        public void Send()
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new QuestProgressRequestProtocolMessagePack(_playerId)).ToList());
        }
    }
}