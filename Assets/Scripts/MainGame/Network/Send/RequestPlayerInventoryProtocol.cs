using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class RequestPlayerInventoryProtocol
    {
        private const short ProtocolId = 3;
        private readonly ISocket _socket;
        private readonly int playerId;

        public RequestPlayerInventoryProtocol(ISocket socket,PlayerConnectionSetting playerConnectionSetting)
        {
            playerId = playerConnectionSetting.PlayerId;
            _socket = socket;
            //接続した時の初回送信
            _socket.OnConnected += Send;
        }
        
        public void Send()
        {
            _socket.Send(MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(playerId)).ToList());
        }
    }
}