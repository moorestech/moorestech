using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendBlockRemoveProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 10;
        private readonly int _playerId;

        
        public SendBlockRemoveProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y)
        {

            _socket.Send(MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(
                _playerId,x,y)).ToList());
        }
    }
}