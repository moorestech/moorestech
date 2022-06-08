using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendBlockInventoryOpenCloseControlProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 16;
        private readonly int _playerId;

        
        public SendBlockInventoryOpenCloseControlProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y,bool isOpen)
        {
            _socket.Send(MessagePackSerializer.Serialize(new BlockInventoryOpenCloseProtocolMessagePack(
                _playerId,x,y,isOpen)).ToList());
        }
    }
}